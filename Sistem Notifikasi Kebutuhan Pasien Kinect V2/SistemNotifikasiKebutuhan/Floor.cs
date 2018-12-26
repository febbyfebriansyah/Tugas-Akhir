using Microsoft.Kinect;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KinectCoordinateMapping
{
    public class Floor
    {
        public float X { get; internal set; }
        public float Y { get; internal set; }
        public float Z { get; internal set; }
        public float W { get; internal set; }

        public Floor(Vector4 floorClipPlane)
        {
            X = floorClipPlane.X; // orientasi X bidang lantai pada ruang 3D
            Y = floorClipPlane.Y; // orientasi Y bidang lantai pada ruang 3D
            Z = floorClipPlane.Z; // orientasi Z bidang lantai pada ruang 3D
            W = floorClipPlane.W; // jarak antara sensor dengan bidang lantai
        }

        public float Height
        {
            get { return W; }
        }

        public double Tilt
        {
            get { return Math.Atan(Z / Y) * (180.0 / Math.PI); }
        }

        public double DistanceFrom(CameraSpacePoint point)
        {
            double numerator = (X * point.X) + (Y * point.Y) + (Z * point.Z) + W;
            double denominator = Math.Sqrt(X * X + Y * Y + Z * Z);

            return numerator / denominator;
        }
    }
}
