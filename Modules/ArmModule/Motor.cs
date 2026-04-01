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
    public class Motor
    {
        public int id;
        public string CustomName;
        public IMyMotorStator Block;
        public double TargetAngle;
        public double Angle { get{return MathHelper.ToDegrees(Block.Angle);} }
        public double Rad { get{return Block.Angle;} }
        public double RPM { get{return Block.TargetVelocityRPM;} set{Block.TargetVelocityRPM = (float)value;} }
        public double MaxRPM;
        public double MaxAcc;
        public bool ActiveControl = false;

        public Motor(BlockCatalogue _catalog, IMyMotorStator _block)
        {
            CustomName = _block.CustomName;
            Block = _block;
            if(!int.TryParse(_catalog.GetBlockConfiguration(Block).Get("general", "motor/id").ToString(), out id))
                throw new Exception($"Motor {CustomName} is missing or has invalid motor/id");
            if(!double.TryParse(_catalog.GetBlockConfiguration(Block).Get("general", "motor/maxrpm").ToString(), out MaxRPM))
                throw new Exception($"Motor {CustomName} is missing or has invalid motor/maxrpm");
            if(!double.TryParse(_catalog.GetBlockConfiguration(Block).Get("general", "motor/maxacc").ToString(), out MaxAcc))
                throw new Exception($"Motor {CustomName} is missing or has invalid motor/maxacc");
            TargetAngle = Angle;
        }


    }
}