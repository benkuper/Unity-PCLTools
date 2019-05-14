using System.Collections.Generic;
using UnityEngine;
using BK.Kinect;
using DataStructures.ViliWonka.KDTree;
using CielaSpike;
using System.Threading;
using System.Collections;

namespace BK.PCL
{
    [RequireComponent(typeof(KinectManager))]
    public class ClusterManager : MonoBehaviour
    {
        KinectManager kinect;

        public ComputeShader pclCompute;
        int filterKernelHandle;

        int totalPoints;
        int threadGroupCount;

        int[] metaData;
        public int numFilteredPoints;

        Vector3[] filteredPoints;
        Vector3[] borderPoints;

        ComputeBuffer pointsBuffer;
        ComputeBuffer filteredBuffer;
        ComputeBuffer excludeBuffer;
        ComputeBuffer borderBuffer;

        ComputeBuffer countBuffer;
        int[] counter;

        [Header("Box Filter")]
        public bool enableBoxFilter = true;
        public Bounds mainBounds;
        public List<Bounds> excludeBounds;
        //excludeArray;

        [Header("DownScaling")]
        [Range(1, 100)]
        public int downScaleFactor = 50;
        [Range(1, 100)]
        public int HDDownScaleFactor = 3;

        [Header("KDTree")]
        public bool enableKDTree;
        KDTree tree;
        KDQuery query;

        [Header("Cluster")]
        public bool enableCluster;

        bool clusterLock;
        public int numClusters;
        [Range(0, 2)]
        public float clusterMaxDist = .03f;
        [Range(.01f, .2f)]
        public float minClusterSize = .3f;
        [Range(1, 100)]
        public int minNeighboursThreshold = 10;

        public float maxCorrespondanceDist = 1;
        public float clusterGhostTime = .3f;

        [Header("Orientation")]
        public bool enableOrientation;
        public float borderRadius = .1f;
        public float aimRadius = .1f;
        public float hdCenterRadius = .2f;

        public Vector3 orientationTarget;
        public float orientationExcludeRadius;

        public List<Cluster> clusters { get; private set; }
        Dictionary<int, Cluster> clusterIdMap;

        public delegate void ClusterEvent(Cluster cluster);
        public event ClusterEvent clusterAdded;
        public event ClusterEvent clusterUpdated;
        public event ClusterEvent clusterGhosted;
        public event ClusterEvent clusterRemoved;

        //Threading
        bool processLock;

        //Debug

        public enum DrawDebug { None, Raw, Clusters, Orientations, All };

        [Header("Debug")]
        public DrawDebug drawDebug;

        void Start()
        {
            kinect = GetComponent<KinectManager>();

            totalPoints = kinect.realWorldMap.Length;
            threadGroupCount = totalPoints / 64; // 64 is the group count in computer shader

            metaData = new int[1];

            filteredPoints = new Vector3[totalPoints];
            borderPoints = new Vector3[5000]; //not more than 5000 points on cluster border !

            Debug.Log("Kinect is here with : " + totalPoints + " point");
            filterKernelHandle = pclCompute.FindKernel("TransformAndFilter");

            pointsBuffer = new ComputeBuffer(totalPoints, 12);
            pclCompute.SetBuffer(filterKernelHandle, "pointsBuffer", pointsBuffer);


            filteredBuffer = new ComputeBuffer(totalPoints, 12, ComputeBufferType.Append);

            pclCompute.SetBuffer(filterKernelHandle, "filteredBuffer", filteredBuffer);

            countBuffer = new ComputeBuffer(1, sizeof(int), ComputeBufferType.IndirectArguments);
            counter = new int[1] { 0 };

            excludeBuffer = new ComputeBuffer(excludeBounds.Count, 24);
            pclCompute.SetBuffer(filterKernelHandle, "excludeBuffer", excludeBuffer);

            borderBuffer = new ComputeBuffer(borderPoints.Length, 12, ComputeBufferType.Append);
            pclCompute.SetBuffer(filterKernelHandle, "borderBuffer", borderBuffer);

            tree = new KDTree(filteredPoints, 0, 16);

            numFilteredPoints = 0;

            query = new KDQuery();

            clusters = new List<Cluster>();
            clusterIdMap = new Dictionary<int, Cluster>();

            
        }

        void Update()
        {
            if(!processLock)
            {
                processPCLCompute();
                StartCoroutine("processThread");
            }

            if (drawDebug == DrawDebug.Raw || drawDebug == DrawDebug.All)
            {
                Color c = new Color(1, 1, 1, .3f);
                for (int i = 0; i < numFilteredPoints; i++)
                {
                    Vector3 p = filteredPoints[i];
                    Debug.DrawLine(p, p + Vector3.up * .005f, c);
                    Debug.DrawLine(p, p + Vector3.right * .005f, c);
                }
            }
        }



        IEnumerator processThread()
        {
            processLock = true;

            Task task;

            //Debug.Log("Process KD Tree");
            this.StartCoroutineAsync(processKDTree(), out task);
            yield return task.Wait();

            //Debug.Log("Process clusters");
            this.StartCoroutineAsync(processClusters(), out task);
            yield return task.Wait();

            processLock = false;

            yield return null;
        }


        void processPCLCompute()
        {
            pclCompute.SetBool("enableBoxFilter", enableBoxFilter);
            pclCompute.SetVector("minPoint", mainBounds.min);
            pclCompute.SetVector("maxPoint", mainBounds.max);
            pclCompute.SetInt("downScaleFactor", downScaleFactor);
            pclCompute.SetInt("currentIndex", 0);

            if (excludeBuffer.count != excludeBounds.Count)
            {
                excludeBuffer.Release();
                excludeBuffer = new ComputeBuffer(excludeBounds.Count, 24);
                pclCompute.SetBuffer(filterKernelHandle, "excludeBuffer", excludeBuffer);
            }

            excludeBuffer.SetData(excludeBounds);

            pclCompute.SetMatrix("transformMat", transform.localToWorldMatrix);

            filteredBuffer.SetCounterValue(0);
            pointsBuffer.SetData(kinect.realWorldMap);

            pclCompute.Dispatch(filterKernelHandle, threadGroupCount, 1, 1);

            ComputeBuffer.CopyCount(filteredBuffer, countBuffer, 0);
            countBuffer.GetData(counter);
            numFilteredPoints = counter[0];
            filteredBuffer.GetData(filteredPoints);

           
        }



        IEnumerator processKDTree()
        {
            if (enableKDTree && enableBoxFilter && numFilteredPoints > 0) tree.Rebuild(numFilteredPoints, 16);
            yield return null;
        }


        IEnumerator processClusters()
        {
            if (!enableKDTree || !enableBoxFilter || !enableCluster)
            {
                yield return null;
            }
            else
            {
                bool[] processedIndices = new bool[numFilteredPoints];
                bool[] noiseIndices = new bool[numFilteredPoints];
                bool[] assignedToCluster = new bool[numFilteredPoints];

                int noiseCount = 0;

                List<Cluster> newClusters = new List<Cluster>();

                for (int i = 0; i < numFilteredPoints; i++)
                {
                    //If we already processed this star, skip it
                    if (processedIndices[i])
                        continue;

                    processedIndices[i] = true;

                    //Todo: will the visited.false stuff be carried over?
                    List<int> neighbourIndices = new List<int>();

                    query.Radius(tree, filteredPoints[i], clusterMaxDist, neighbourIndices);

                    //If not enough neighbours, label as noise and continue.
                    if (neighbourIndices.Count < minNeighboursThreshold)
                    {
                        noiseIndices[i] = true;
                        noiseCount++;
                        //assignedToCluster[i] = false;
                    }
                    else
                    {
                        //Else, start new cluster.
                        List<int> newClusterIndices = new List<int>();

                        Vector3 clusterCenter = filteredPoints[i];
                        Vector3 boundsMin = filteredPoints[i];
                        Vector3 boundsMax = filteredPoints[i];

                        assignedToCluster[i] = true;
                        newClusterIndices.Add(i);

                        //Expanding the new cluster
                        var seedSet = neighbourIndices;

                        List<int> propagationNeighbourIndices = new List<int>();

                        while (seedSet.Count > 0)
                        {
                            var currentSeedIndice = seedSet[0];

                            if (!processedIndices[currentSeedIndice])
                            {
                                processedIndices[currentSeedIndice] = true;

                                propagationNeighbourIndices.Clear();

                                //if(use2DTree) query2D.Radius(tree2D, filteredXZPoints[currentSeedIndice], clusterMaxDist, propagationNeighbourIndices);
                                //else
                                query.Radius(tree, filteredPoints[currentSeedIndice], clusterMaxDist, propagationNeighbourIndices);

                                if (propagationNeighbourIndices.Count >= minNeighboursThreshold)
                                    seedSet.AddRange(propagationNeighbourIndices);

                                if (!assignedToCluster[currentSeedIndice])
                                {
                                    assignedToCluster[currentSeedIndice] = true;
                                    newClusterIndices.Add(currentSeedIndice);

                                    clusterCenter += filteredPoints[currentSeedIndice];
                                    boundsMin = Vector3.Min(boundsMin, filteredPoints[currentSeedIndice]);
                                    boundsMax = Vector3.Max(boundsMax, filteredPoints[currentSeedIndice]);
                                }
                            }

                            //Doing this to avoid infinite loop
                            seedSet.Remove(currentSeedIndice);
                        }


                        if (newClusterIndices.Count > 0) clusterCenter /= newClusterIndices.Count;
                        Bounds clusterBounds = new Bounds();
                        clusterBounds.SetMinMax(boundsMin, boundsMax);

                        if (clusterBounds.size.x > minClusterSize && clusterBounds.size.y > minClusterSize && clusterBounds.size.z > minClusterSize)
                        {
                            Cluster newCluster = new Cluster(newClusterIndices.ToArray(), -1, clusterCenter, clusterBounds);
                            newClusters.Add(newCluster);
                        }
                    }
                }


                yield return Ninja.JumpToUnity;
                if (drawDebug == DrawDebug.Clusters || drawDebug == DrawDebug.All)
                {
                    foreach (Cluster c in clusters)
                    {
                        foreach (int index in c.indices)
                        {
                            Debug.DrawLine(filteredPoints[index], filteredPoints[index] + Vector3.up * .007f, c.color);
                            Debug.DrawLine(filteredPoints[index], filteredPoints[index] + Vector3.right * .007f, c.color);
                        }
                    }
                }
                yield return Ninja.JumpBack;



                if (enableOrientation) processClusterOrientations(newClusters);

                yield return Ninja.JumpToUnity;
                processClusterCorrespondance(newClusters);
                numClusters = clusters.Count;
                yield return Ninja.JumpBack;
            }
        }


        IEnumerator processClusterOrientations(List<Cluster> newClusters)
        {
            Bounds innerBounds = new Bounds(mainBounds.center, mainBounds.size - new Vector3(1, 0, 1) * borderRadius); //avoid removing y

            //Replace filteredPoints with HD result from computeShader
            yield return Ninja.JumpToUnity;
            pclCompute.SetInt("downScaleFactor", HDDownScaleFactor);
            pclCompute.SetFloat("borderRadius", borderRadius);
            yield return Ninja.JumpBack;


            foreach (Cluster c in newClusters)
            {
                yield return Ninja.JumpToUnity;
                pclCompute.SetVector("minPoint", c.bounds.min);
                pclCompute.SetVector("maxPoint", c.bounds.max);
                pclCompute.SetVector("mainMinPoint", mainBounds.min);
                pclCompute.SetVector("mainMaxPoint", mainBounds.max);

                pclCompute.SetInt("currentIndex", 0);

                filteredBuffer.SetCounterValue(0);
                borderBuffer.SetCounterValue(0);
                countBuffer.SetCounterValue(0);

                pclCompute.Dispatch(filterKernelHandle, threadGroupCount, 1, 1);

                ComputeBuffer.CopyCount(filteredBuffer, countBuffer, 0);
                countBuffer.GetData(counter);
                int numClusterPoints = counter[0];
                filteredBuffer.GetData(filteredPoints);

                ComputeBuffer.CopyCount(borderBuffer, countBuffer, 0);
                countBuffer.GetData(counter);
                int numBorderPoints = Mathf.Min(counter[0], borderPoints.Length);
                borderBuffer.GetData(borderPoints);
                yield return Ninja.JumpBack;


                Vector3 clusterP1 = new Vector3();
                for (int i = 0; i < numBorderPoints; i++)
                {
                    clusterP1 += borderPoints[i];
                    if (drawDebug == DrawDebug.Orientations || drawDebug == DrawDebug.All) Debug.DrawLine(borderPoints[i] + Vector3.left * .0001f, borderPoints[i] + Vector3.left * .005f, Color.red);
                }

                if (numBorderPoints > 0) clusterP1 /= numBorderPoints;
                Vector3 p2Aim = clusterP1 + (c.center - clusterP1) * 2.2f;

                Vector3 clusterP2 = new Vector3();
                int numInP2Radius = 0;

                Vector3 hdCenter = new Vector3();
                int numInCenterRadius = 0;
                for (int i = 0; i < numClusterPoints; i++)
                {
                    if (Vector3.Distance(c.center, filteredPoints[i]) < hdCenterRadius)
                    {
                        hdCenter += filteredPoints[i];
                        numInCenterRadius++;
                    }

                    if (Vector3.Distance(p2Aim, filteredPoints[i]) < aimRadius)
                    {
                        clusterP2 += filteredPoints[i];
                        numInP2Radius++;

                       // if (drawDebug == DrawDebug.Orientations || drawDebug == DrawDebug.All) Debug.DrawLine(filteredPoints[i] + Vector3.right * .0001f, filteredPoints[i] + Vector3.right * .005f, Color.blue);
                    }
                }

                if (numInCenterRadius > 0) hdCenter /= numInCenterRadius;
                else hdCenter = c.center;

                if (numInP2Radius > 0) clusterP2 /= numInP2Radius;
                else clusterP2 = p2Aim;

                c.center = hdCenter;

                c.ray = new Ray(c.center, (clusterP2 - c.center).normalized);

                yield return Ninja.JumpToUnity;
                if (drawDebug == DrawDebug.Orientations || drawDebug == DrawDebug.All)
                {
                    for (int i = 0; i < numClusterPoints; i++) Debug.DrawLine(filteredPoints[i], filteredPoints[i] + Vector3.down * .001f, c.color);
                    Debug.DrawRay(c.ray.origin, c.ray.direction, Color.grey);

                }
                yield return Ninja.JumpBack;
            }

            yield return null;
        }


        void processClusterCorrespondance(List<Cluster> newClusters)
        {
            //Assign all new clusters ids by looping through oldClusers and finding closest newcluster

            List<Cluster> clustersToRemove = new List<Cluster>(); //we will put here all clusters that are replaced by new ones, and ghost clusters that are too old
            List<Cluster> clustersToGhost = new List<Cluster>();
            Dictionary<Cluster, Cluster> clustersUpdateMap = new Dictionary<Cluster, Cluster>();

            foreach (Cluster c in clusters)
            {
                float minDist = maxCorrespondanceDist;
                Cluster closestNewCluster = null;

                foreach (Cluster nc in newClusters)
                {
                    if (nc.id != -1) continue; //already assigned

                    float dist = Vector3.Distance(c.center, nc.center);
                    if (dist < minDist)
                    {
                        minDist = dist;
                        closestNewCluster = nc;
                    }
                }

                if (closestNewCluster != null)
                {
                    //cluster updated
                    closestNewCluster.id = c.id;
                    closestNewCluster.color = Color.HSVToRGB((closestNewCluster.id / 10.0f) % 1.0f, 1, 1);
                    clustersToRemove.Add(c);
                }
                else
                {
                    //Debug.Log("Cluster " + c.id + " no correspondance " + c.isGhost() + " > " + c.timeAtGhosted);
                    if (!c.isGhost())
                    {
                        //Debug.Log("Ghost cluster " + c.id);
                        c.timeAtGhosted = Time.time; //start ghosting here
                        c.color = Color.grey;
                        clusterGhosted?.Invoke(c);
                    }
                    else if (Time.time > c.timeAtGhosted + clusterGhostTime)
                    {
                        //Debug.Log("Remove cluster");
                        clusterRemoved?.Invoke(c);
                        clustersToRemove.Add(c); //dead cluster
                        clusterIdMap.Remove(c.id);

                        // Debug.Log("Cluster removed " + c.id);
                    }
                }
            }

            foreach (Cluster c in clustersToRemove) clusters.Remove(c); //remove all transfered and dead clusters

            newClusters.AddRange(clusters); //add ghost clusters to end of list
            clusters = new List<Cluster>(newClusters); //replace main list with new list

            foreach (Cluster nc in newClusters)
            {
                if (nc.id == -1)
                {
                    //Cluster added
                    nc.id = Cluster.idCounter++;
                    nc.color = Color.HSVToRGB((nc.id / 10.0f) % 1.0f, 1, 1);

                    clusterIdMap.Add(nc.id, nc);
                    clusterAdded?.Invoke(nc);
                }
                else
                {
                    //Cluster updated
                    clusterUpdated?.Invoke(nc);
                }
            }
        }


       


        private void OnApplicationQuit()
        {
            filteredBuffer.Release();
            pointsBuffer.Release();
            excludeBuffer.Release();
            countBuffer.Release();
        }

        void OnDrawGizmosSelected()
        {
            //Gizmos.color = Color.yellow;
           // Gizmos.DrawWireCube(mainBounds.center, mainBounds.size);

            //Gizmos.color = Color.red;
           // for (int i = 0; i < excludeBounds.Count; i++)
           // {
           //     Gizmos.DrawWireCube(excludeBounds[i].center, excludeBounds[i].size);
           // }

            if (Application.isPlaying)
            {
                if (drawDebug == DrawDebug.Clusters || drawDebug == DrawDebug.All)
                {
                    foreach (Cluster c in clusters)
                    {
                        Gizmos.color = c.color;
                        Gizmos.DrawWireSphere(c.center, .02f);
                        Gizmos.DrawWireCube(c.bounds.center, c.bounds.size);
                    }
                }
            }


            Gizmos.color = new Color(1, 0, 1);
            Gizmos.DrawWireCube(orientationTarget, Vector3.one * .05f);
        }

        public void RotatePointAroundPivot(Vector3 point, Vector3 pivot, Quaternion rot)
        {
            transform.localPosition = rot * (point - pivot) + pivot;
            transform.localRotation *= rot;
        }

    }
    
}
