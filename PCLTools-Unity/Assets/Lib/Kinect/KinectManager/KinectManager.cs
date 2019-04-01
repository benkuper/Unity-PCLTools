using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Windows.Kinect;

namespace BK.Kinect
{
    public class KinectManager : MonoBehaviour
    {
        public bool enableColor;
        public bool enableDepth;

        public int colorWidth { get; private set; }
        public int colorHeight { get; private set; }
        public int depthWidth { get; private set; }
        public int depthHeight { get; private set; }

        private KinectSensor sensor;
        private MultiSourceFrameReader reader;

        public Texture2D colorTexture { get; private set; }
        public byte[] colorData { get; private set; }
        public ushort[] depthMap { get; private set; }
        public CameraSpacePoint[] realWorldMap { get; private set; }

        void Awake()
        {
            sensor = KinectSensor.GetDefault();

            if (sensor != null)
            {
                reader = sensor.OpenMultiSourceFrameReader(FrameSourceTypes.Color | FrameSourceTypes.Depth);

                var colorFrameDesc = sensor.ColorFrameSource.CreateFrameDescription(ColorImageFormat.Rgba);
                colorWidth = colorFrameDesc.Width;
                colorHeight = colorFrameDesc.Height;

                colorTexture = new Texture2D(colorFrameDesc.Width, colorFrameDesc.Height, TextureFormat.RGBA32, false);
                colorData = new byte[colorFrameDesc.BytesPerPixel * colorFrameDesc.LengthInPixels];

                var depthFrameDesc = sensor.DepthFrameSource.FrameDescription;
                depthWidth = depthFrameDesc.Width;
                depthHeight = depthFrameDesc.Height;
                depthMap = new ushort[depthFrameDesc.LengthInPixels];
                realWorldMap = new CameraSpacePoint[depthMap.Length];


                if (!sensor.IsOpen) sensor.Open();
            }
        }

        void Update()
        {
            readFrame();
        }

        void readFrame()
        {
            if (reader == null) return;

            var frame = reader.AcquireLatestFrame();
            if (frame == null) return;

            if (enableColor)
            {
                var colorFrame = frame.ColorFrameReference.AcquireFrame();
                if (colorFrame != null)
                {
                    colorFrame.CopyConvertedFrameDataToArray(colorData, ColorImageFormat.Rgba);
                    colorTexture.LoadRawTextureData(colorData);
                    colorTexture.Apply();

                    colorFrame.Dispose();
                    colorFrame = null;
                }
            }

            if (enableDepth)
            {
                var depthFrame = frame.DepthFrameReference.AcquireFrame();
                if (depthFrame != null)
                {
                    depthFrame.CopyFrameDataToArray(depthMap);
                    sensor.CoordinateMapper.MapDepthFrameToCameraSpace(depthMap, realWorldMap);

                    depthFrame.Dispose();
                    depthFrame = null;
                }
            }

            frame = null;
        }

        void OnApplicationQuit()
        {
            if (reader != null)
            {
                reader.Dispose();
                reader = null;
            }

            if (sensor != null)
            {
                if (sensor.IsOpen)
                {
                    sensor.Close();
                }

                sensor = null;
            }
        }
    }
}