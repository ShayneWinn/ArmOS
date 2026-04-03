using Sandbox.Common.ObjectBuilders.Definitions;
using Sandbox.Game.AI;
using Sandbox.Game.Entities;
using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI.Ingame;
using Sandbox.ModAPI.Interfaces;
using SpaceEngineers.Game.ModAPI.Ingame;
using System;
using System.CodeDom;
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
    public class ActiveMotor
    {
        // CONSTRUCTORS
        public ActiveMotor(IMyMotorStator _block, BlockCatalogue _catalog)
        {
            State = MotorStates.OFF;
            Block = _block;
            if(!int.TryParse(_catalog.GetBlockConfiguration(Block).Get("general", "motor/id").ToString(), out id))
                throw new Exception($"Motor {CustomName} is missing or has invalid motor/id");
            if(!double.TryParse(_catalog.GetBlockConfiguration(Block).Get("general", "motor/maxrpm").ToString(), out MaxRPM))
                throw new Exception($"Motor {CustomName} is missing or has invalid motor/maxrpm");
            if(!double.TryParse(_catalog.GetBlockConfiguration(Block).Get("general", "motor/maxacc").ToString(), out MaxAcc))
                throw new Exception($"Motor {CustomName} is missing or has invalid motor/maxacc");
            TargetAngle = Angle;
        }

        // PUBLIC
        public IMyMotorStator Block {get;}
        public readonly int id;
        public string CustomName {get {return Block.CustomName;} }
        public double TargetAngle;
        public double TargetRPM;
        public double Angle{ get{return Block.Angle;} }
        public double Rad{ get{return Block.Angle;} }
        public double Deg{ get{return MathHelper.ToDegrees(Block.Angle);} }
        public double RPM{ get{return Block.TargetVelocityRPM;} set{Block.TargetVelocityRPM = (float)value;} }
        public double MaxRPM;
        public double MaxAcc;
        public MotorStates State;

        public void Update(double dt) {
            if(State == ActiveMotor.MotorStates.ACTIVE) {
                double delta = mod(TargetAngle - Deg + 180, 360) - 180;
                var degps = delta / dt;
                var trpm = MathHelper.Clamp(degps/6, -MaxRPM, MaxRPM);
                var drpm = trpm - RPM;
                if(Math.Abs(drpm) > MaxAcc * dt) {
                    //Mother.Print($"{motor.CustomName} Excceded max Acc {drpm:F2}/{motor.MaxRPM:F2}");
                    drpm = MathHelper.Clamp(drpm, -MaxAcc * dt, MaxAcc * dt);
                }
                RPM += drpm;
            }
            else if (State == ActiveMotor.MotorStates.MOVING) {
                double delta = mod(TargetAngle - Deg + 180, 360) - 180;
                if (Math.Abs(delta) <= 1){
                    Stop();
                }
            }
        }

        public void RotateToAngle(double angle, double rpm) 
        {
            this.TargetAngle = angle; this.TargetRPM = rpm;
            Block.RotateToAngle(MyRotationDirection.AUTO, (float)TargetAngle, (float)TargetRPM);
        }

        public void Activate(double angle = double.NaN) {
            if(double.IsNaN(angle))
                angle = Angle;
            TargetAngle = angle;
            State = ActiveMotor.MotorStates.ACTIVE;
        }

        /// <summary>
        /// Move motor to desired position WITHOUT active control
        /// </summary>
        public void Start(double angle, double seconds) 
        {
            double delta = mod(angle - Deg + 180, 360) - 180;
            var degps = delta / seconds;
            var rpm = MathHelper.Clamp(degps/6, -MaxRPM, MaxRPM);

            RPM = (float)rpm;
            TargetAngle = angle;
            State = ActiveMotor.MotorStates.MOVING;
        }

        public void Stop(){
            RPM = 0;
            State = ActiveMotor.MotorStates.OFF;
        }

        // INTERNALS
        public enum MotorStates {
            OFF,
            MOVING,
            ACTIVE,
        }
        private double mod (double a, double n) { return (a % n + n) % n; }
    }
}