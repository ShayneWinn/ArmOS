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
    //
    //              .-.
    //         _.-'( o ).
    //    .--:' _.-''-'. \.
    //   ( {} )'        \. \.
    //    '--'            \. \.
    //     \  \           / /\ \
    //      \  \          \/  \/
    //      .\  \-.        ArmOS
    //     /  ,-.  \
    //     \  `-'  /
    //      :-._.-:
    //____ /       \ __________
    //    '|_______|'          \
    //                          \
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

            Motors = new List<ActiveMotor>();

            Catalogue.GetBlocksByName<IMyMotorStator>("#arm/motor");
            foreach (var motor in Catalogue.GetBlocksByName<IMyMotorStator>("#arm/motor"))
            {
                Mother.Print("$Found motor: " + motor.CustomName);
                Motors.Add(new ActiveMotor(motor, Catalogue));
            }

            Motors.Sort((a, b) => { return a.id.CompareTo(b.id);});


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
            foreach (ActiveMotor motor in Motors) {
                if(motor.State == ActiveMotor.MotorStates.ACTIVE) {
                    double dt = Mother.Runtime.TimeSinceLastRun.TotalSeconds;
                    double delta = mod(motor.TargetAngle - motor.Deg + 180, 360) - 180;
                    var degps = delta / dt;
                    var trpm = MathHelper.Clamp(degps/6, -motor.MaxRPM, motor.MaxRPM);
                    var drpm = trpm - motor.RPM;
                    if(Math.Abs(drpm) > motor.MaxAcc * dt) {
                        //Mother.Print($"{motor.CustomName} Excceded max Acc {drpm:F2}/{motor.MaxRPM:F2}");
                        drpm = MathHelper.Clamp(drpm, -motor.MaxAcc * dt, motor.MaxAcc * dt);
                    }
                    motor.RPM += drpm;
                }
                else if (motor.RPM != 0) {
                    double delta = mod(motor.TargetAngle - motor.Deg + 180, 360) - 180;
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
            foreach(ActiveMotor motor in Motors) 
            {
                if(motor.CustomName.Equals(name))
                    return motor.Block;
            }
            return ret;
        }


        //  //=========\\
        //  ||  Logic  ||
        //  \\=========//


        private List<ActiveMotor> Motors;

        CommandBus Bus;
        BlockCatalogue Catalogue;

        private double[] InverseKinematics(Vector3D pos)
        {
            double x = pos.X;
            double y = pos.Y;
            
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

        private Vector3D ForwardKinematics() {
            double baseAngle = Motors[0].Rad;
            double shoulderAngle = Motors[1].Rad;
            double elbowAngle = Motors[2].Rad;
            double armLength = 3f;

            double baseLength = (Math.Sin(shoulderAngle) * armLength * 2) + 1;
            double y = baseLength * Math.Sin(baseAngle + Math.PI/2);
            double x = baseLength * Math.Cos(baseAngle + Math.PI/2);

            return new Vector3D(x, y, 0);
        }

        private double[] targetAngles;

        /// <summary>
        /// Move to desired position WITHOUT active control
        /// </summary>
        public string MoveTo(PathPoint target)
        {
            targetAngles = InverseKinematics(target.Position);
            Mother.Print($"Moving {Motors.Count} to {targetAngles.Length} angles");
            if (Motors.Count != targetAngles.Length)
                return "";

            foreach (ActiveMotor motor in Motors){
                MoveMotor(motor, targetAngles[motor.id], target.t);
            }

            return $"Moving ({target.Position.X}, {target.Position.Y}) over {target.t}s";
        }
        /// <summary>
        /// Move motor to desired position WITHOUT active control
        /// </summary>
        public void MoveMotor(ActiveMotor motor, double angle, double seconds)
        {
            double delta = mod(angle - motor.Deg + 180, 360) - 180;
            var degps = delta / seconds;
            var rpm = MathHelper.Clamp(degps/6, -motor.MaxRPM, motor.MaxRPM);

            motor.RPM = (float)rpm;
            motor.TargetAngle = angle;
            motor.State = ActiveMotor.MotorStates.MOVING;
            motor.Block.Enabled = true;
            
            Mother.Print($"{motor.CustomName} => {angle:F2} @ {rpm:F2}");
        }

        /// <summary>
        /// Move to desired position WITH active control
        /// </summary>
        public string StartTo(PathPoint target)
        {
            if(positionQueue.Count != 0){
                Mother.Print("Clearing Position Queue");
                positionQueue.Clear();
            }
            PathPoint current = new PathPoint(ForwardKinematics());
            positionQueue.Add(current); positionQueue.Add(target);
            
            MoveAll();
            return $"Moving ({target.Position.X}, {target.Position.Y}) over {target.t}s";
        }
        private void StopMove() {
            foreach (ActiveMotor motor in Motors) {
                motor.RPM = 0;
                motor.State = ActiveMotor.MotorStates.ACTIVE;
            }
        }

        private double mod (double a, double n) { return (a % n + n) % n; }




        /// <summary>
        /// Home the arm to its default position.
        /// </summary>
        public void HomeArm()
        {
            MoveTo(new PathPoint(0, 1, 5));
        }

        public void InitTest() {
            positionQueue.Add(new PathPoint(0, 1, 4));
            positionQueue.Add(new PathPoint(0, 3, 2));
            positionQueue.Add(new PathPoint(3, 3, 2));
            positionQueue.Add(new PathPoint(3, -3, 2));
            positionQueue.Add(new PathPoint(3, 0, 2, 1));
            positionQueue.Add(new PathPoint(3, 3, 2));
            positionQueue.Add(new PathPoint(-3, 3, 2));
            positionQueue.Add(new PathPoint(0, 1, 5));
        }

        private List<PathPoint> positionQueue = new List<PathPoint>();
        public string MoveAll()
        {
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

            targetAngles = InverseKinematics(target.Position);
            foreach (ActiveMotor motor in Motors)
            {
                motor.TargetAngle = targetAngles[motor.id];
                motor.State = ActiveMotor.MotorStates.ACTIVE;
            }

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
// you dont want to know how long that ascii art took...