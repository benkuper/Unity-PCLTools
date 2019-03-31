using System.Collections.Generic;
using UnityEngine;
using BK.Kinect;
using DataStructures.ViliWonka.KDTree;

namespace BK.PCL
{
    [RequireComponent(typeof(KinectManager))]
    public class ClusterManager : MonoBehaviour
    {
        KinectManager kinect;

        public ComputeShader pclCompute;
        int kernelHandle;

        int totalPoints;
        int threadGroupCount;

        int[] metaData;
        public int numHDFilteredPoints;
        public int numLDFilteredPoints;

        Vector3[] HDFilteredPoints;
        Vector3[] LDFilteredPoints;

        ComputeBuffer pointsBuffer;
        ComputeBuffer HDFilteredBuffer;
        ComputeBuffer LDFilteredBuffer;

        ComputeBuffer HDCountBuffer;
        int[] HDCounter;
        ComputeBuffer LDCountBuffer;
        int[] LDCounter;

        [Header("Box Filter")]
        public bool enableBoxFilter = true;
        public Vector3 boxCenter = Vector3.zero;
        public Vector3 boxSize = Vector3.one;

        [Header("DownScaling")]
        [Range(1, 100)]
        public int HDDownScaleFactor = 3;
        [Range(1, 100)]
        public int LDDownScaleFactor = 50;

        [Header("KDTree")]
        public bool enableKDTree;
        KDTree HDTree;
        KDTree LDTree;
        KDQuery query;


        [Header("Cluster")]
        public bool enableCluster;
        bool clusterLock;
        public int numClusters;
        [Range(0,  2)]
        public float clusterMaxDist = .03f;
        [Range(.01f,.2f)]
        public float minClusterSize = .3f;
        [Range(1, 100)]
        public int minNeighboursThreshold = 10;


        [Header("Tracking")]
        public float maxCorrespondanceDist = 1;
        public float clusterGhostTime = .3f;

        
        public List<Cluster> clusters { get; private set; }
        Dictionary<int, Cluster> clusterIdMap;

        public delegate void ClusterEvent(Cluster cluster);
        public event ClusterEvent clusterAdded;
        public event ClusterEvent clusterUpdated;
        public event ClusterEvent clusterGhosted;
        public event ClusterEvent clusterRemoved;


        public enum DrawDebug { None, Raw, Clusters, All };

        [Header("Debug")]
        public DrawDebug drawDebug;

        void Start()
        {
            kinect = GetComponent<KinectManager>();

            totalPoints = kinect.realWorldMap.Length;
            threadGroupCount = totalPoints / 64; // 64 is the group count in computer shader

            metaData = new int[1];

            HDFilteredPoints = new Vector3[totalPoints];
            LDFilteredPoints = new Vector3[totalPoints];

            Debug.Log("Kinect is here with : " + totalPoints + " point");
            kernelHandle = pclCompute.FindKernel("TransformAndFilter");

            pointsBuffer = new ComputeBuffer(totalPoints, 12);
            pclCompute.SetBuffer(kernelHandle, "pointsBuffer", pointsBuffer);

            HDFilteredBuffer = new ComputeBuffer(totalPoints, 12, ComputeBufferType.Append);
            LDFilteredBuffer = new ComputeBuffer(totalPoints, 12, ComputeBufferType.Append);

            pclCompute.SetBuffer(kernelHandle, "HDFilteredBuffer", HDFilteredBuffer);
            pclCompute.SetBuffer(kernelHandle, "LDFilteredBuffer", LDFilteredBuffer);

            HDCountBuffer = new ComputeBuffer(1, sizeof(int), ComputeBufferType.IndirectArguments);
            HDCounter = new int[1] { 0 };

            LDCountBuffer = new ComputeBuffer(1, sizeof(int), ComputeBufferType.IndirectArguments);
            LDCounter = new int[1] { 0 };

            HDTree = new KDTree(HDFilteredPoints, 0, 16);
            LDTree = new KDTree(LDFilteredPoints, 0, 16);

            numHDFilteredPoints = 0;
            numLDFilteredPoints = 0;

            query = new KDQuery();

            clusters = new List<Cluster>();
            clusterIdMap = new Dictionary<int, Cluster>();
        }

        void Update()
        {
            processPCLCompute();
            processKDTree();
            processClusters();
        }


        void processPCLCompute()
        {
            pclCompute.SetBool("enableBoxFilter", enableBoxFilter);
            pclCompute.SetVector("minPoint", boxCenter - boxSize / 2);
            pclCompute.SetVector("maxPoint", boxCenter + boxSize / 2);
            pclCompute.SetInt("HDDownScaleFactor", HDDownScaleFactor);
            pclCompute.SetInt("LDDownScaleFactor", LDDownScaleFactor);
            pclCompute.SetInt("currentIndex", 0);

            pclCompute.SetMatrix("transformMat", transform.localToWorldMatrix);

            HDFilteredBuffer.SetCounterValue(0);
            LDFilteredBuffer.SetCounterValue(0);

            pointsBuffer.SetData(kinect.realWorldMap);

            pclCompute.Dispatch(kernelHandle,threadGroupCount, 1, 1);

            
            ComputeBuffer.CopyCount(HDFilteredBuffer, HDCountBuffer, 0);
            HDCountBuffer.GetData(HDCounter);
            numHDFilteredPoints = HDCounter[0];
            HDFilteredBuffer.GetData(HDFilteredPoints);

            ComputeBuffer.CopyCount(LDFilteredBuffer, LDCountBuffer, 0);
            LDCountBuffer.GetData(LDCounter);
            numLDFilteredPoints = LDCounter[0];
            LDFilteredBuffer.GetData(LDFilteredPoints);
            

            if (drawDebug == DrawDebug.Raw || drawDebug == DrawDebug.All)
            {
                Color c = new Color(1, 1, 1, .3f);
                for (int i = 0; i < numHDFilteredPoints; i++)
                {
                    Vector3 p = HDFilteredPoints[i];
                    Debug.DrawLine(p, p + Vector3.up * .01f, c);
                    Debug.DrawLine(p, p + Vector3.right * .01f, c);
                }
            }
        }

        void processKDTree()
        {
            if (!enableKDTree || !enableBoxFilter) return;
            if(numHDFilteredPoints > 0) HDTree.Rebuild(numHDFilteredPoints, 16);
            if(numLDFilteredPoints > 0) LDTree.Rebuild(numLDFilteredPoints, 16);
        }
        

        void processClusters()
        {
            if (!enableKDTree || !enableBoxFilter || !enableCluster) return;

            bool[] processedIndices = new bool[numLDFilteredPoints];
            bool[] noiseIndices = new bool[numLDFilteredPoints];
            bool[] assignedToCluster = new bool[numLDFilteredPoints];

            int noiseCount = 0;

            List<Cluster> newClusters = new List<Cluster>();

            for(int i = 0;i< numLDFilteredPoints; i++)
            {
                //If we already processed this star, skip it
                if (processedIndices[i])
                    continue;

                processedIndices[i] = true;

                //Todo: will the visited.false stuff be carried over?
                List<int> neighbourIndices = new List<int>();

               query.Radius(LDTree, LDFilteredPoints[i], clusterMaxDist, neighbourIndices);

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

                    Vector3 clusterCenter = LDFilteredPoints[i];
                    Vector3 boundsMin = LDFilteredPoints[i];
                    Vector3 boundsMax = LDFilteredPoints[i];

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
                            query.Radius(LDTree, LDFilteredPoints[currentSeedIndice], clusterMaxDist, propagationNeighbourIndices);
                            
                            if (propagationNeighbourIndices.Count >= minNeighboursThreshold)
                                seedSet.AddRange(propagationNeighbourIndices);

                            if (!assignedToCluster[currentSeedIndice])
                            {
                                assignedToCluster[currentSeedIndice] = true;
                                newClusterIndices.Add(currentSeedIndice);

                                clusterCenter += LDFilteredPoints[currentSeedIndice];
                                boundsMin = Vector3.Min(boundsMin, LDFilteredPoints[currentSeedIndice]);
                                boundsMax = Vector3.Max(boundsMax, LDFilteredPoints[currentSeedIndice]);
                            }
                        }

                        //Doing this to avoid infinite loop
                        seedSet.Remove(currentSeedIndice);
                    }


                    clusterCenter /= newClusterIndices.Count;
                    Bounds clusterBounds = new Bounds();
                    clusterBounds.SetMinMax(boundsMin, boundsMax);

                    if (clusterBounds.size.x > minClusterSize && clusterBounds.size.y > minClusterSize && clusterBounds.size.z > minClusterSize)
                    {
                        Cluster newCluster = new Cluster(newClusterIndices.ToArray(), -1, clusterCenter, clusterBounds);
                        newClusters.Add(newCluster);
                    }
                }
            }

            processClusterCorrespondance(newClusters);
            numClusters = clusters.Count;

            if (drawDebug == DrawDebug.Clusters || drawDebug == DrawDebug.All)
            {
                foreach(Cluster c in clusters)
                {
                    foreach (int index in c.indices)
                    {
                        Debug.DrawLine(LDFilteredPoints[index], LDFilteredPoints[index] + Vector3.up * .01f, c.color);
                        Debug.DrawLine(LDFilteredPoints[index], LDFilteredPoints[index] + Vector3.right * .01f, c.color);
                    }
                }
            }
        }
        

        void processClusterCorrespondance(List<Cluster> newClusters)
        {
            //Assign all new clusters ids by looping through oldClusers and finding closest newcluster

            List<Cluster> clustersToRemove = new List<Cluster>(); //we will put here all clusters that are replaced by new ones, and ghost clusters that are too old
            List<Cluster> clustersToGhost = new List<Cluster>();
            Dictionary<Cluster, Cluster> clustersUpdateMap = new Dictionary<Cluster, Cluster>();
            
            foreach(Cluster c in clusters)
            {
                float minDist = maxCorrespondanceDist;
                Cluster closestNewCluster = null;

                foreach(Cluster nc in newClusters)
                {
                    if (nc.id != -1) continue; //already assigned

                    float dist = Vector3.Distance(c.center, nc.center);
                    if(dist < minDist)
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
                        Debug.Log("Ghost cluster " + c.id);
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

                        Debug.Log("Cluster removed " + c.id);
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


        //Helpers 
        public Cluster getClusterWithID(int id)
        {
            return clusterIdMap.ContainsKey(id) ? clusterIdMap[id] : null;
        }


        private void OnApplicationQuit()
        {
            HDFilteredBuffer.Release();
            LDFilteredBuffer.Release();
            pointsBuffer.Release();
            HDCountBuffer.Release();
            LDCountBuffer.Release();
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireCube(boxCenter, boxSize);

            if(Application.isPlaying)
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

        }
    }
   
}
