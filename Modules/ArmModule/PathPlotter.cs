using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using VRage.Network;
using VRageMath;

namespace IngameScript
{
    public class PathPoint
    {
        public readonly double x;
        public readonly double y;
        public readonly double t;
        public readonly double v;
        public Vector3D Position;
        public PathPoint(Vector3D position, double t=0, double v=0)
        {
            this.Position = position;
            this.v = v;
            this.x = position.X;
            this.y = position.Y;
            this.t = t;
        }
        public PathPoint(double x, double y, double t=1, double v=0) : 
        this(new Vector3D(x, y, 0), t, v)
        {}
    }

    class PathPlotter
    {
        List<PathPoint> points;
        public PathPlotter(List<PathPoint> points)
        {
            this.points = points;
        }

        public List<PathPoint> Plot(List<PathPoint> points, double tStep = 0.1)
        {
            if(points.Count <= 0)
            {
                return new List<PathPoint>();
            }
            if(points.Count == 1)
            {
                return points;
            }
            
            List<PathPoint> ret = new List<PathPoint>();
            ret.Add(points[0]);
            for(int i = 0; i < points.Count-1; i++)
            {
                double t = 0;
                while(t < points[i+1].t)
                {
                    //Vector3D pos = Lerp(points[i], points[i+1], t/points[i+1].t);
                    //ret.Add(new PathPoint(pos, Vector3D.Zero, tStep));
                    ret.Add(new PathPoint(
                        InterpolateSegment(points[i], points[i+1], t/points[i+1].t),
                        tStep,
                        0
                    ));
                    t += tStep;
                }
            }
            ret.Add(points[points.Count-1]);
            return ret;
        }

        private Vector3D Lerp(PathPoint p1, PathPoint p2, double t)
        {
            double easedT = t;
            // Apply smooth ease-in-out for acceleration and deceleration
            if (p1.v == 0 && p2.v == 0) 
            {
                easedT = EaseInOut(t);
            }
            else if(p1.v != 0 && p2.v == 0)
            {
                easedT = EaseOut(t);
            }
            else if(p1.v == 0 && p2.v != 0)
            {
                easedT = EaseIn(t);
            }
            return p1.Position + ((p2.Position - p1.Position) * easedT);
        }

        private double EaseIn(double t)
        {
            // Cubic ease-in: slow start and then accelerate
            return t * t * t;
        }

        private double EaseOut(double t)
        {
            // Cubic ease-out: start fast and slow down at end
            double inv = 1 - t;
            return 1 - (inv * inv * inv);
        }

        private double EaseInOut(double t)
        {
            // Cubic ease-in-out: smooth acceleration and deceleration
            return t * t * (3 - 2 * t);
        }

        public PathPoint GetStateAtTime(double t)
        {
            int currentSegment = 0;
            double elapsed = 0;
            while (points[currentSegment].t + elapsed < t)
            {
                elapsed += points[currentSegment].t;
                currentSegment++;
                if (currentSegment >= points.Count)
                {
                    return new PathPoint
                    (
                        new Vector3D(points[points.Count - 1].x, points[points.Count - 1].y, 0),
                        0,
                        0
                    );
                }
            }

            double segmentProgress = (t - elapsed) / points[currentSegment].t;
            return new PathPoint(InterpolateSegment(
                points[currentSegment],
                points[currentSegment + 1],
                segmentProgress
            ), 1, 0);
        }

        public Vector3D InterpolateSegment(PathPoint p1, PathPoint p2, double t)
        {
            Vector3 start = new Vector3((float)p1.x, (float)p1.y, 0);
            Vector3 end = new Vector3((float)p2.x, (float)p2.y, 0);
            Vector3 direction = (end - start).Normalized();
            
            double t2 = t * t;
            double t3 = t2 * t;

            double h00 = 2 * t3 - 3 * t2 + 1;
            double h10 = t3 - 2 * t2 + t;
            double h01 = -2 * t3 + 3 * t2;
            double h11 = t3 - t2;

            Vector3 p1VelVec = direction * (float)p1.v;
            Vector3 p2VelVec = direction * (float)p2.v;

            Vector3 position = start * (float)h00 + 
                               p1VelVec * (float)h10 * (float)p2.t + 
                               end * (float)h01 + 
                               p2VelVec * (float)h11 * (float)p2.t;

            double h00d = 6*t2 - 2*t;
            double h10d = 3*t2 - 4*t + 1;
            double h01d = -6*t2 + 6*t;
            double h11d = 3*t2 - 2*t;

            Vector3 velocityVec = start * (float)h00d + 
                                  p1VelVec * (float)h10d * (float)p2.t + 
                                  end * (float)h01d + 
                                  p2VelVec * (float)h11d * (float)p2.t;

            return position;
        }
    }
}