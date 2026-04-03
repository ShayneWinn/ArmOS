using Sandbox.Game.Entities;
using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI.Ingame;
using Sandbox.ModAPI.Interfaces;
using SpaceEngineers.Game.ModAPI.Ingame;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
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
using VRageMath;

namespace IngameScript
{
    /// <summary>
    /// The ActiveMotorModule extension module.
    /// </summary>
    public class ActiveMotorModule: BaseExtensionModule
    {
        //  //===========\\
        //  ||  Members  ||
        //  \\===========//
        public List<ActiveMotor> Motors;
        private BlockCatalogue Catalogue;
        private CommandBus Bus;
        //  //==========\\
        //  ||  MOTHER  ||
        //  \\==========//

        /// <summary>
        /// Constructor. In most cases, you should use the boot method to set up this module.
        /// </summary>
        /// <param name="mother"></param>
        public ActiveMotorModule(Mother mother) : base(mother) {
            //
        }

        /// <summary>
        /// Boot the module. This is where you should register commands, subscribe 
        /// to events, and reference other modules registered with Mother.
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
            RegisterCommand(new AddMotorsCommand(this));
            RegisterCommand(new ActivateMotorsCommand(this));
            RegisterCommand(new DeactivateMotorsCommand(this));
            RegisterCommand(new ListCommand(this));
        }



        /// <summary>
        /// Run the module. This method is called each program cycle. You should limit 
        /// use of this method to essential tasks that need to run frequently.
        /// </summary>
        public override void Run()
        {
            double dt = Mother.Runtime.TimeSinceLastRun.TotalSeconds;
            foreach (ActiveMotor motor in Motors) {
                if(motor.State != ActiveMotor.MotorStates.OFF)
                    motor.Update(dt);
            }
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

        //  //==========\\
        //  ||  Public  ||
        //  \\==========//


        //  //=========\\
        //  ||  Logic  ||
        //  \\=========//
    }
}
