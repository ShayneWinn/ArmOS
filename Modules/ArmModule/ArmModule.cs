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

        private string[] motorNames =
        {
            "RotorBase",
            "RotorWrist",
            "HingeShoulder",
            "HingeElbowA",
            "HingeElbowB",
            "HingeWrist",
        };
        private Dictionary<string, IMyMotorStator> motors;
        private Dictionary<string, PIDController> motorControllers;

        CommandBus bus;
        BlockCatalogue catalogue;

        public IMyMotorStator GetMotorByName(string name)
        {
            if (motors.ContainsKey(name))
                return motors[name];
            else
                return null;
        }

        /// <summary>
        /// Constructor. In most cases, you should use the boot method to set up this module.
        /// </summary>
        /// <param name="mother"></param>
        public ArmModule(Mother mother) : base(mother) {
            
        }

        /// <summary>
        /// Boot the module. This is where you should register commands, subscribe 
        /// to events, and reference other modules registered with Mother.
        /// </summary>
        public override void Boot()
        {
            this.bus = Mother.GetModule<CommandBus>();
            this.catalogue = Mother.GetModule<BlockCatalogue>();

            this.motors = new Dictionary<string, IMyMotorStator>();
            this.motorControllers = new Dictionary<string, PIDController>();

            foreach (string motorName in motorNames)
            {
                IMyMotorStator motor = catalogue.GetBlocksByName<IMyMotorStator>(motorName.Trim()).ElementAtOrDefault(0);
                if(motor == null)
                {
                    Mother.Print($"Error: could not find motor with name '{motorName}'");
                    continue;
                } 
                motors[motorName] = motor;
                motorControllers[motorName] = new PIDController(1, 0, 0);
            }

            RegisterCommand(new HomeArmCommand(this));
            RegisterCommand(new RotorRotateCommand(this));
            RegisterCommand(new MoveArmCommand(this));
            RegisterCommand(new MoveAllCommand(this));

            
            

            

            positionQueue.Add(new Position(0, 1, 1));
            positionQueue.Add(new Position(0, 2, 1));
            positionQueue.Add(new Position(0, 3, 1));
            positionQueue.Add(new Position(0, 4, 1));
            positionQueue.Add(new Position(2, 4, 1.8));
            positionQueue.Add(new Position(2, 2, 1.8));
            positionQueue.Add(new Position(2, 0, 1.8));
            positionQueue.Add(new Position(2, -2, 1.8));
            positionQueue.Add(new Position(2, -4, 1.8));
            positionQueue.Add(new Position(2, 0, 4));
            positionQueue.Add(new Position(0, 4, 4));
            positionQueue.Add(new Position(0, 1, 6));
        }

        public string MoveTo(double x, double y, double seconds)
        {
            double armlength = 3f;
            double baselength = Math.Sqrt(x * x + y * y);
            double baseAngle = Math.Atan2(y, x) - Math.PI/2; // compensate for rotor orientation
            double shoulderAngle = Math.Asin( (baselength-1)/2 / armlength );
            double elbowangle = (Math.PI/2) - shoulderAngle;

            bus.RunTerminalCommand($"rotor/rotate {motorNames[0]} {MathHelper.ToDegrees(baseAngle)} --time={seconds}");
            bus.RunTerminalCommand($"rotor/rotate {motorNames[1]} {-MathHelper.ToDegrees(baseAngle)} --time={seconds}");
            bus.RunTerminalCommand($"rotor/rotate {motorNames[2]} {MathHelper.ToDegrees(shoulderAngle)} --time={seconds}");
            bus.RunTerminalCommand($"rotor/rotate {motorNames[3]} {MathHelper.ToDegrees(elbowangle)} --time={seconds}");
            bus.RunTerminalCommand($"rotor/rotate {motorNames[4]} {MathHelper.ToDegrees(elbowangle)} --time={seconds}");
            bus.RunTerminalCommand($"rotor/rotate {motorNames[5]} {MathHelper.ToDegrees(shoulderAngle)} --time={seconds}");
            

            return $"Moving ({x}, {y}) over {seconds} seconds...";
        }

        public string StartMotor(IMyMotorStator motor, double angle, double speed)
        {

            motor.RotateToAngle(MyRotationDirection.AUTO, (float)angle, (float)speed);

            //motor.TargetVelocityRPM = (float)speed;

            return $"Moving {motor.CustomName} to {angle} degrees at {speed} rpm...";
        }

        /// <summary>
        /// Home the arm to its default position.
        /// </summary>
        public void HomeArm()
        {
            MoveTo(0, 1, 5);
        }

        private void StopMotor(IMyMotorStator motor)
        {
            motor.TargetVelocityRad = 0;
            motor.Enabled = false;
        }

        private bool isAtTarget(IMyMotorStator motor, double targetAngle)
        {
            return Math.Abs(motor.Angle - targetAngle) <= 1;
        }


        
        private class Position
        {
            public readonly double x;
            public readonly double y;
            public readonly double t;
            public Position(double x, double y, double t=1)
            {
                this.x = x;
                this.y = y;
                this.t = t;
            }
        }
        private List<Position> positionQueue = new List<Position>();
        public void MoveAll()
        {
            if (positionQueue.Count == 0)
                return;
            Position target = positionQueue[0];
            MoveTo(target.x, target.y, target.t);
            positionQueue.RemoveAt(0);
            Mother.Wait(() => MoveAll(), target.t + .1);
        }
    
        /// <summary>
        /// Run the module. This method is called each program cycle. You should limit 
        /// use of this method to essential tasks that need to run frequently.
        /// </summary>
        public override void Run()
        {
            //
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
