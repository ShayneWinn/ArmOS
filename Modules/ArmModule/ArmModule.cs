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

            Motors = new List<Motor>();

            Catalogue.GetBlocksByName<IMyMotorStator>("#arm/motor");
            foreach (var motor in Catalogue.GetBlocksByName<IMyMotorStator>("#arm/motor"))
            {
                Mother.Print("$Found motor: " + motor.CustomName);
                Motors.Add(new Motor(Catalogue, motor));
            }
            Motors.OrderBy(i => i.id);


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
            foreach (Motor motor in Motors) {
                if(motor.ActiveControl) {
                    double dt = Mother.Runtime.TimeSinceLastRun.TotalSeconds;
                    double delta = mod(motor.TargetAngle - motor.Angle + 180, 360) - 180;
                    var degps = delta / dt;
                    var trpm = MathHelper.Clamp(degps/6, -motor.MaxRPM, motor.MaxRPM);
                    var drpm = trpm - motor.RPM;
                    if(Math.Abs(drpm) > motor.MaxAcc * dt) {
                        Mother.Print($"{motor.CustomName} Excceded max Acc {drpm:F2}/{motor.MaxRPM:F2}");
                        drpm = MathHelper.Clamp(drpm, -motor.MaxAcc * dt, motor.MaxAcc * dt);
                    }
                    motor.RPM += drpm;
                }
                else if (motor.RPM != 0) {
                    double delta = mod(motor.TargetAngle - motor.Angle + 180, 360) - 180;
                    if (Math.Abs(delta) <= 2){
                        motor.RPM = 0;
                    }
                }
            }
        }


        //  //==========\\
        //  ||  Public  ||
        //  \\==========//

        public IMyMotorStator GetMotorByName(string name)
        {
            IMyMotorStator ret = null;
            foreach(Motor motor in Motors) 
            {
                if(motor.CustomName.Equals(name))
                    return motor.Block;
            }
            return ret;
        }


        //  //=========\\
        //  ||  Logic  ||
        //  \\=========//


        private List<Motor> Motors;

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
            targetAngles = InverseKinematics(x, y);
            Mother.Print($"Moving {Motors.Count} to {targetAngles.Length} angles");
            if (Motors.Count != targetAngles.Length)
                return "";

            for (int i = 0; i < targetAngles.Length; i++)
                MoveMotor(Motors[i], targetAngles[i], seconds);
            
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
        /// Move motor to desired position WITHOUT active control
        /// </summary>
        public void MoveMotor(Motor motor, double angle, double seconds)
        {
            double delta = mod(angle - motor.Angle + 180, 360) - 180;
            var degps = delta / seconds;
            var rpm = MathHelper.Clamp(degps/6, -motor.MaxRPM, motor.MaxRPM);

            motor.RPM = (float)rpm;
            motor.TargetAngle = angle;
            motor.ActiveControl = false;
            motor.Block.Enabled = true;
            
            Mother.Print($"{motor.CustomName} => {angle:F2} @ {rpm:F2}");
        }
        // DEPRECIATED
        private bool IsMotorAtTarget(IMyMotorStator motor, double angle)
        {
            var actual = MathHelper.ToDegrees(motor.Angle);
            double delta = mod(angle - actual + 180, 360) - 180;
            return Math.Abs(delta) <= 2;
        }
        private void StopMotor(Motor motor)
        {
            motor.RPM = 0;
            motor.ActiveControl = false;
        }

        /// <summary>
        /// Move to desired position WITH active control
        /// </summary>
        public void StartTo(PathPoint point)
        {
            targetAngles = InverseKinematics(point.x, point.y);
            for (int i = 0; i < targetAngles.Length; i++)
            {
                Motors[i].TargetAngle = targetAngles[i];
                Motors[i].ActiveControl = true;
            }

        }
        private void StopMove() {
            foreach (Motor motor in Motors) {
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
