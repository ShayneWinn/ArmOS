using Sandbox.Common.ObjectBuilders.Definitions;
using Sandbox.Game.AI;
using Sandbox.Game.Entities;
using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI.Ingame;
using Sandbox.ModAPI.Interfaces;
using SpaceEngineers.Game.ModAPI.Ingame;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using VRage;
using VRage.Collections;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.GUI.TextPanel;
using VRage.Game.ModAPI.Ingame;
using VRage.Game.ModAPI.Ingame.Utilities;
using VRage.Game.ObjectBuilders.Definitions;
using VRage.Scripting;
using VRage.Scripting.MemorySafeTypes;
using VRageMath;

namespace IngameScript
{

    /// <summary>
    /// The ArmModule extension module.
    /// </summary>
    
    public class ArmModule: BaseExtensionModule
    {

        //  //==========\\
        //  ||  MOTHER  ||
        //  \\==========//

        /// <summary>
        /// Constructor. In most cases, you should use the boot method to set up this module.
        /// </summary>
        /// <param name="mother"></param>
        public ArmModule(Mother mother) : base(mother) {
            
        }

        /// <summary>
        /// Boot the module, register commands, subscribe to events,
        /// and reference other modules registered with Mother.
        /// Find all motors with the tag "arm/motor" and initialize them.
        /// </summary>
        public override void Boot()
        {
            Bus = Mother.GetModule<CommandBus>();
            Catalogue = Mother.GetModule<BlockCatalogue>();

            MotorNames = new List<string>();
            Motors = new Dictionary<string, IMyMotorStator>();

            Catalogue.GetBlocksByName<IMyMotorStator>("#arm/motor");
            foreach (var motor in Catalogue.GetBlocksByName<IMyMotorStator>("#arm/motor"))
            {
                Mother.Print("$Found motor: " + motor.CustomName);
                MotorNames.Add(motor.CustomName);
                Motors[motor.CustomName] = motor;
            }
            MotorNames.Sort((a, b) => {
                var aid = this.Catalogue.GetBlockConfiguration(this.Motors[a]).Get("general", "arm/id").ToInt32(-1);
                var bid = this.Catalogue.GetBlockConfiguration(this.Motors[b]).Get("general", "arm/id").ToInt32(-1);
                if (aid == -1)
                    Mother.Print($"Error: motor '{a}' is missing configuration 'arm/id'");
                if (bid == -1)
                    Mother.Print($"Error: motor '{b}' is missing configuration 'arm/id'");
                return aid.CompareTo(bid);
            });


            //MOTHER... HEAR ME!!!
            RegisterCommand(new HomeArmCommand(this));
            RegisterCommand(new RotorRotateCommand(this));
            RegisterCommand(new MoveArmCommand(this));
            RegisterCommand(new MoveAllCommand(this));
        }

        /// <summary>
        /// Run the module. This method is called each program cycle. You should limit 
        /// use of this method to essential tasks that need to run frequently.
        /// </summary>
        public override void Run()
        {
            if(this.ActiveControl) {
                if (targetAngles.Length > 0){
                    for(int i = 0; i < MotorNames.Count; i++) {
                        IMyMotorStator motor = Motors[MotorNames[i]];
                        double angle = targetAngles[i];

                        var actual = MathHelper.ToDegrees(motor.Angle);
                        double delta = mod(angle - actual + 180, 360) - 180;
                        var degps = delta / Mother.Runtime.TimeSinceLastRun.TotalSeconds;
                        var rpm = degps/6;
                        motor.TargetVelocityRPM = (float)rpm;
                        Mother.Print($"{motor.CustomName} {actual:F2} => {angle:F2} @ {rpm:F2}");
                    }
                } else {
                    StopMove();
                }
            }
        }


        //  //==========\\
        //  ||  Public  ||
        //  \\==========//

        public IMyMotorStator GetMotorByName(string name)
        {
            if (Motors.ContainsKey(name))
                return Motors[name];
            else
                return null;
        }


        //  //=========\\
        //  ||  Logic  ||
        //  \\=========//

        private bool ActiveControl = false;

        private List<string> MotorNames;
        private Dictionary<string, IMyMotorStator> Motors;

        CommandBus Bus;
        BlockCatalogue Catalogue;

        private double[] InverseKinematics(double x, double y)
        {
            double armlength = 3f;
            double baselength = Math.Sqrt(x * x + y * y);
            double baseAngle = Math.Atan2(y, x) - Math.PI/2; // compensate for rotor orientation
            double shoulderAngle = Math.Asin( (baselength-1)/2 / armlength );
            double elbowangle = (Math.PI/2) - shoulderAngle;

            baseAngle = MathHelper.ToDegrees(baseAngle);
            shoulderAngle = MathHelper.ToDegrees(shoulderAngle);
            elbowangle = MathHelper.ToDegrees(elbowangle);

            return new double[] {
                baseAngle,
                shoulderAngle,
                elbowangle,
                elbowangle,
                shoulderAngle,
                baseAngle
            };
        }

        private double[] targetAngles;

        /// <summary>
        /// Move to desired position WITHOUT active control
        /// </summary>
        public string MoveTo(double x, double y, double seconds)
        {
            ActiveControl = false;
            targetAngles = InverseKinematics(x, y);
            Mother.Print($"Moving {Motors.Count} to {targetAngles.Length} angles");
            for (int i = 0; i < targetAngles.Length; i++)
            {
                var motor = Motors[MotorNames[i]];
                var targetAngle = targetAngles[i];
                MoveMotor(motor, targetAngle, seconds);
            }
            
            return $"Moving ({x}, {y}) over {seconds}s";
        }
        /// <summary>
        /// Move to desired position WITHOUT active control
        /// </summary>
        public string MoveTo(PathPoint target)
        {
            return MoveTo(target.x, target.y, target.t);
        }
        /// <summary>
        /// Move to desired position WITHOUT active control
        /// </summary>
        public void MoveMotor(IMyMotorStator motor, double angle, double seconds)
        {
            var actual = MathHelper.ToDegrees(motor.Angle);
            double delta = mod(angle - actual + 180, 360) - 180;
            var degps = delta / seconds;
            var rpm = MathHelper.Clamp(degps/6, -5, 5);
            motor.TargetVelocityRPM = (float)rpm;
            motor.Enabled = true;
            Mother.GetModule<ActivityMonitor>().RegisterBlock(
                motor,
                block => IsMotorAtTarget(block as IMyMotorStator, angle),
                block => StopMotor(block as IMyMotorStator)
            );
            
            Mother.Print($"{motor.CustomName} => {angle:F2} @ {rpm:F2}");
        }
        private bool IsMotorAtTarget(IMyMotorStator motor, double angle)
        {
            var actual = MathHelper.ToDegrees(motor.Angle);
            double delta = mod(angle - actual + 180, 360) - 180;
            return Math.Abs(delta) <= 2;
        }
        private void StopMotor(IMyMotorStator motor)
        {
            motor.TargetVelocityRad = 0;
            //motor.Enabled = false;
            //Mother.GetModule<ActivityMonitor>().UnregisterBlock(motor);
        }

        /// <summary>
        /// Move to desired position WITH active control
        /// </summary>
        private void StartTo(PathPoint point)
        {
            ActiveControl = true;
            targetAngles = InverseKinematics(point.x, point.y);
        }
        private void StopMove() {
            ActiveControl = false;
            foreach (IMyMotorStator motor in Motors.Values) {
                StopMotor(motor);
            }
        }

        private double mod (double a, double n) { return (a % n + n) % n; }




        /// <summary>
        /// Home the arm to its default position.
        /// </summary>
        public void HomeArm()
        {
            MoveTo(0, 1, 5);
        }

        private List<PathPoint> positionQueue = new List<PathPoint>();
        public string MoveAll()
        {
            positionQueue.Add(new PathPoint(0, 1, 4));
            positionQueue.Add(new PathPoint(0, 3, 2));
            positionQueue.Add(new PathPoint(3, 3, 2));
            positionQueue.Add(new PathPoint(3, -3, 2));
            positionQueue.Add(new PathPoint(3, 0, 2, 1));
            positionQueue.Add(new PathPoint(3, 3, 2));
            positionQueue.Add(new PathPoint(-3, 3, 2));
            positionQueue.Add(new PathPoint(0, 1, 5));


            List<PathPoint> processedPoints;
            PathPlotter plotter = new PathPlotter();
            Mother.Print($"Processing {positionQueue.Count} points");
            processedPoints = plotter.Plot(positionQueue, .1);
            positionQueue = processedPoints;
            PopPosition();
            return $"Moving {positionQueue.Count} points";
        }
        private void PopPosition()
        {
            if (positionQueue.Count == 0)
            {   
                StopMove();
                return;
            }
            PathPoint target = positionQueue[0];

            StartTo(target);
            Mother.Wait(() => PopPosition(), target.t);
            Mother.Print($"{target.x:F4}, {target.y:F4}, {target.z:F4}");
            positionQueue.RemoveAt(0);
        }

        /// <summary>
        /// Handle events to which this module is subscribed.
        /// </summary>
        /// <param name="e"></param>
        /// <param name="eventData"></param>
        public override void HandleEvent(IEvent e, object eventData)
        {
            //
        }
    }
}
