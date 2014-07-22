/* --------------------------------------------------------------------------------
 * Gear: Parallax Inc. Propeller Debugger
 * Copyright 2007 - Robert Vandiver
 * --------------------------------------------------------------------------------
 * PropellerCPU.cs
 * Provides the body object of a propeller emulator
 * --------------------------------------------------------------------------------
 *  This program is free software; you can redistribute it and/or modify
 *  it under the terms of the GNU General Public License as published by
 *  the Free Software Foundation; either version 2 of the License, or
 *  (at your option) any later version.
 *
 *  This program is distributed in the hope that it will be useful,
 *  but WITHOUT ANY WARRANTY; without even the implied warranty of
 *  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 *  GNU General Public License for more details.
 *
 *  You should have received a copy of the GNU General Public License
 *  along with this program; if not, write to the Free Software
 *  Foundation, Inc., 59 Temple Place, Suite 330, Boston, MA  02111-1307  USA
 * --------------------------------------------------------------------------------
 */

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Data;
using System.Text;
using System.Windows.Forms;

using Gear;
using Gear.PluginSupport;
using Gear.GUI;

/// @todo Document Gear.EmulationCore namespace.
///
namespace Gear.EmulationCore
{
    /// @brief Identifiers for hub operations.
    /// 
    public enum HubOperationCodes : uint
    {
        HUBOP_CLKSET  = 0,  //!< Setting the clock
        HUBOP_COGID   = 1,  //!< Getting the Cog ID
        HUBOP_COGINIT = 2,  //!< Start or restart a Cog by ID
        HUBOP_COGSTOP = 3,  //!< Stop Cog by its ID
        HUBOP_LOCKNEW = 4,  //!< Check out new semaphore and get its ID
        HUBOP_LOCKRET = 5,  //!< Return semaphore back to semaphore pool, releasing it for future LOCKNEW requests.
        HUBOP_LOCKSET = 6,  //!< Set semaphore to true and get its previous state
        HUBOP_LOCKCLR = 7   //!< Clear semaphore to false and get its previous state
    }

    /// @brief Possible pin states.
    /// 
    public enum PinState
    {
        FLOATING,   //!< Pin Floating
        OUTPUT_LO,  //!< Output Low (0V)
        OUTPUT_HI,  //!< Output Hi (3.3V)
        INPUT_LO,   //!< Input Low (0V)
        INPUT_HI,   //!< Input Hi (3.3V)
    }

    /// @todo Document Gear.EmulationCore.PropellerCPU class.
    /// 
    public partial class PropellerCPU : Propeller.DirectMemory
    {
        /// @brief Name of Constants for setting Clock.
        /// 
        static private string[] CLKSEL = new string[] {
            "RCFAST",   // Internal fast oscillator:    $00000001
            "RCSLOW",   // Internal slow oscillator:    $00000002
            "XINPUT",   // External clock/oscillator:   $00000004
            "PLL1X",    // External frequency times 1:  $00000040
            "PLL2X",    // External frequency times 2:  $00000080
            "PLL4X",    // External frequency times 4:  $00000100
            "PLL8X",    // External frequency times 8:  $00000200
            "PLL16X"    // External frequency times 16: $00000400
        };

        /// @brief Name of external clock constants.
        /// 
        static private string[] OSCM = new string[] {
            "XINPUT+",  // External clock/oscillator:     $00000004
            "XTAL1+",   // External low-speed crystal:    $00000008
            "XTAL2+",   // External medium-speed crystal: $00000010 
            "XTAL3+"    // External high-speed crystal:   $00000020
        };

        private Cog[] Cogs;         //!< @todo Document member Gear.EmulationCore.PropellerCPU.Cogs
        private byte[] ResetMemory; //!< @todo Document member Gear.EmulationCore.PropellerCPU.ResetMemory

        private bool[] LocksAvailable;  //!< @todo Document member Gear.EmulationCore.PropellerCPU.LocksAvailable
        private bool[] LocksState;      //!< @todo Document member Gear.EmulationCore.PropellerCPU.LocksState

        private ClockSource[] ClockSources; //!< @todo Document member Gear.EmulationCore.PropellerCPU.ClockSources
        private SystemXtal CoreClockSource; //!< @todo Document member Gear.EmulationCore.PropellerCPU.CoreClockSource

        private uint RingPosition;  //!< @todo Document member Gear.EmulationCore.PropellerCPU.RingPosition
        private ulong PinHi;        //!< @todo Document member Gear.EmulationCore.PropellerCPU.PinHi
        private ulong PinFloat;     //!< @todo Document member Gear.EmulationCore.PropellerCPU.PinFloat
        private uint SystemCounter; //!< @todo Document member Gear.EmulationCore.PropellerCPU.SystemCounter

        private uint XtalFreq;          //!< @todo Document member Gear.EmulationCore.PropellerCPU.XtalFreq
        private uint CoreFreq;          //!< @todo Document member Gear.EmulationCore.PropellerCPU.CoreFreq
        private byte ClockMode;         //!< @todo Document member Gear.EmulationCore.PropellerCPU.ClockMode
        private PinState[] PinStates;   //!< @todo Document member Gear.EmulationCore.PropellerCPU.PinStates

        private bool pinChange;     //!< @todo Document member Gear.EmulationCore.PropellerCPU.pinChange    

        private double Time;        //!< @todo Document member Gear.EmulationCore.PropellerCPU.Time

        private Emulator emulator;  //!< @todo Document member Gear.EmulationCore.PropellerCPU.emulator

        private List<PluginBase> TickHandlers;      //!< @todo Document member Gear.EmulationCore.PropellerCPU.TickHandlers
        private List<PluginBase> PinNoiseHandlers;  //!< @todo Document member Gear.EmulationCore.PropellerCPU.PinNoiseHandlers
        private List<PluginBase> PlugIns;           //!< @todo Document member Gear.EmulationCore.PropellerCPU.PlugIns

        //Expose constants declarations to use on the project. 
        public const int TOTAL_COGS   = 8;          //!< @todo Document member Gear.EmulationCore.PropellerCPU.TOTAl_COGS
        public const int TOTAL_LOCKS  = 8;          //!< @todo Document member Gear.EmulationCore.PropellerCPU.TOTAL_LOCKS
        public const int TOTAL_PINS   = 64;         //!< @todo Document member Gear.EmulationCore.PropellerCPU.TOTAL_PINS
        public const int TOTAL_MEMORY = 0x10000;    //!< @todo Document member Gear.EmulationCore.PropellerCPU.TOTAL_MEMORY
        public const int TOTAL_RAM    = 0x8000;     //!< @todo Document member Gear.EmulationCore.PropellerCPU.TOTAL_RAM

        /// @brief PropellerCPU Constructor.
        /// 
        /// @param em Reference to the Gear.GUI.Emulator instance controlling this PropellerCPU.
        /// 
        public PropellerCPU(Emulator em)
        {
            emulator = em;
            Cogs = new Cog[TOTAL_COGS];             // 8 general purpose cogs

            for (int i = 0; i < TOTAL_COGS; i++)
                Cogs[i] = null;

            PinHi = 0;
            PinFloat = 0xFFFFFFFFFFFFFFFF;

            TickHandlers = new List<PluginBase>();
            PinNoiseHandlers = new List<PluginBase>();
            PlugIns = new List<PluginBase>();

            Time = 0;
            RingPosition = 0;
            LocksAvailable = new bool[TOTAL_LOCKS]; // 8 general purpose semaphors
            LocksState = new bool[TOTAL_LOCKS];

            Memory = new byte[TOTAL_MEMORY];        // 64k of memory (top 32k read-only bios)

            PinStates = new PinState[TOTAL_PINS];   // We have 64 pins we will be passing on

            ClockSources = new ClockSource[TOTAL_COGS];
            CoreClockSource = new SystemXtal();

            // Put rom it top part of main ram.
            Resources.BiosImage.CopyTo(Memory, TOTAL_MEMORY - TOTAL_RAM);
        }

        /// @todo Document method Gear.EmulationCore.PropellerBreakPoint().
        /// 
        public void BreakPoint()
        {
            emulator.BreakPoint();
        }

        /// @todo Document property Gear.EmulationCore.PropellerCPU.EmulatorTime
        /// 
        public double EmulatorTime
        {
            get
            {
                return Time;
            }
        }

        /// @todo Document property Gear.EmulationCore.PropellerCPU.Counter
        /// 
        public uint Counter
        {
            get
            {
                return SystemCounter;
            }
        }

        /// @todo Document property Gear.EmulationCore.PropellerCPU.Ring
        /// 
        public uint Ring
        {
            get
            {
                return RingPosition;
            }
        }

        /// @todo Document property Gear.EmulationCore.PropellerCPU.DIRA
        /// 
        public uint DIRA
        {
            get
            {
                uint direction = 0;
                for (int i = 0; i < Cogs.Length; i++)
                {
                    if (Cogs[i] != null)
                        direction |= Cogs[i].DIRA;
                }
                return direction;
            }
        }

        /// @todo Document property Gear.EmulationCore.PropellerCPU.DIRB
        /// 
        public uint DIRB
        {
            get
            {
                uint direction = 0;
                for (int i = 0; i < Cogs.Length; i++)
                    if (Cogs[i] != null)
                        direction |= Cogs[i].DIRB;
                return direction;
            }
        }

        /// @todo Document property Gear.EmulationCore.PropellerCPU.INA
        /// 
        public uint INA
        {
            get
            {
                uint localOut = 0;
                uint directionOut = DIRA;

                for (int i = 0; i < Cogs.Length; i++)
                    if (Cogs[i] != null)
                        localOut |= Cogs[i].OUTA;

                return (localOut & directionOut) | ((uint)PinHi & ~directionOut);
            }
        }

        /// @todo Document property Gear.EmulationCore.PropellerCPU.INB
        /// 
        public uint INB
        {
            get
            {
                uint localOut = 0;
                uint directionOut = DIRB;

                for (int i = 0; i < Cogs.Length; i++)
                    if (Cogs[i] != null)
                        localOut |= Cogs[i].OUTB;

                return (localOut & directionOut) | ((uint)(PinHi >> 32) & ~directionOut);
            }
        }

        /// @brief Property for total DIR of pins (P63..P0).
        /// Only take Pin use of ACTIVES cogs, making OR between them.
        public ulong DIR
        {
            get
            {
                ulong direction = 0;
                for (int i = 0; i < Cogs.Length; i++)
                    if (Cogs[i] != null)
                        direction |= Cogs[i].DIR;
                return direction;
            }
        }

        /// @brief Property for total IN of pins (P63..P0).
        /// Only take Pin use of ACTIVES cogs.
        public ulong IN
        {
            get
            {
                ulong localOut = 0;
                ulong directionOut = DIR;   //get total pins Dir (P63..P0)

                for (int i = 0; i < Cogs.Length; i++)
                {
                    if (Cogs[i] == null)
                        continue;
                    localOut |= Cogs[i].OUT;
                }

                return (localOut & directionOut) | (PinHi & ~directionOut);
            }
        }

        /// @brief Property for total OUT of pins (P63..P0).
        /// Only take Pin use of ACTIVES cogs, making OR between them.
        public ulong OUT
        {
            get
            {
                ulong localOut = 0;

                for (int i = 0; i < Cogs.Length; i++)
                {
                    if (Cogs[i] != null)
                        localOut |= Cogs[i].OUT;
                }

                return localOut;
            }
        }

        /// @todo Document property Gear.EmulationCore.PropellerCPU.Floating
        /// 
        public ulong Floating
        {
            get
            {
                return PinFloat & ~DIR;
            }
        }

        /// @todo Document property Gear.EmulationCore.PropellerCPU.Locks
        /// 
        public byte Locks
        {
            get
            {
                byte b = 0;

                for (int i = 0; i < TOTAL_LOCKS; i++)
                    b |= (byte)(LocksState[i] ? (1 << i) : 0);

                return b;
            }
        }

        /// @todo Document property Gear.EmulationCore.PropellerCPU.Clock
        /// 
        public string Clock
        {
            get
            {
                string mode = "";

                if ((ClockMode & 0x80) != 0)
                    mode += "RESET+";
                if ((ClockMode & 0x40) != 0)
                    mode += "PLL+";
                if ((ClockMode & 0x20) != 0)
                    mode += OSCM[(ClockMode & 0x18) >> 3];

                return mode + CLKSEL[ClockMode & 0x7];
            }
        }

        /// @todo Document property Gear.EmulationCore.PropellerCPU.XtalFrequency
        /// 
        public uint XtalFrequency
        {
            get { return XtalFreq; }
        }

        /// @todo Document property Gear.EmulationCore.PropellerCPU.CoreFrequency
        /// 
        public uint CoreFrequency
        {
            get { return CoreFreq; }
        }

        /// @todo Document property Gear.EmulationCore.PropellerCPU.LocksFree
        /// 
        public byte LocksFree
        {
            get
            {
                byte b = 0;

                for (int i = 0; i < TOTAL_LOCKS; i++)
                    b |= (byte)(LocksAvailable[i] ? (1 << i) : 0);

                return b;
            }
        }

        /// @todo Document method Gear.EmulationCore.PropellerCPU.Initialize().
        /// 
        public void Initialize(byte[] program)
        {
            if (program.Length > TOTAL_RAM)
                return;

            for (int i = 0; i < TOTAL_RAM; i++)
                Memory[i] = 0;

            program.CopyTo(Memory, 0);
            ResetMemory = new byte[Memory.Length];
            Memory.CopyTo(ResetMemory, 0);

            CoreFreq = DirectReadLong(0);
            ClockMode = DirectReadByte(4);

            if ((ClockMode & 0x18) != 0)
            {
                int pll = (ClockMode & 7) - 3;
                if (pll >= 0)
                    XtalFreq = CoreFreq / (uint)(1 << pll);
                else if (pll == -1)
                    XtalFreq = CoreFreq;
            }

            // Write termination code (just in case)
            uint address = (uint)DirectReadWord(0x0A) - 8;  // Load the end of the binary
            DirectWriteLong(address, 0xFFFFF9FF);
            DirectWriteLong(address + 4, 0xFFFFF9FF);

            Reset();
        }

        /// @todo Document method Gear.EmulationCore.PropellerCPU.GetCog().
        /// 
        public Cog GetCog(int id)
        {
            if (id > Cogs.Length)
                return null;

            return Cogs[id];
        }

        /// @todo Document method Gear.EmulationCore.PropellerCPU.GetPLL().
        /// 
        public PLLGroup GetPLL(uint cog)
        {
            if (cog >= ClockSources.Length)
                return null;

            return (PLLGroup)ClockSources[cog];
        }

        /// @brief Include a plugin in active plugin list of propeller instance
        /// It see if the plugin exist already to insert or not.
        /// @param[in] mod Compiled plugin reference to include
        public void IncludePlugin(PluginBase mod)
        {
            if (!(PlugIns.Contains(mod)))
                PlugIns.Add(mod);
        }

        /// @brief Remove a plugin from the active plugin list of propeller instance
        /// Only if the plugin exists on the list, this method removes from it.
        /// @param[in] mod Compiled plugin reference to remove
        public void RemovePlugin(PluginBase mod)
        {
            if (PlugIns.Contains(mod))
                PlugIns.Remove(mod);
        }

        /// @brief Add a plugin to be notified on clock ticks
        /// It see if the plugin exist already to insert or not.
        /// @param mod Compiled plugin reference to include
        public void NotifyOnClock(PluginBase mod)
        {
            if (!(TickHandlers.Contains(mod)))
                TickHandlers.Add(mod);
        }

        /// @brief Remove a plugin from the clock notify list
        /// Only if the plugin exists on the list, this method removes from it.
        /// @param mod Compiled plugin reference to remove
        public void RemoveOnClock(PluginBase mod)
        {
            if (TickHandlers.Contains(mod))
                TickHandlers.Remove(mod);
        }

        /// @brief Add a plugin to be notified on pin changes
        /// It see if the plugin exist already to insert or not.
        /// @param mod Compiled plugin reference to include
        public void NotifyOnPins(PluginBase mod)
        {
            if (!(PinNoiseHandlers.Contains(mod)))
                PinNoiseHandlers.Add(mod);
        }

        /// @brief Remove a plugin from the pin changed notify list
        /// Only if the plugin exists on the list, this method removes from it.
        /// @param mod Compiled plugin reference to remove
        public void RemoveOnPins(PluginBase mod)
        {
            if (PinNoiseHandlers.Contains(mod))
                PinNoiseHandlers.Remove(mod);
        }

        /// @todo Document method Gear.EmulationCore.PropellerCPU.SetClockMode().
        /// 
        public void SetClockMode(byte mode)
        {
            ClockMode = mode;

            if ((mode & 0x80) != 0)
            {
                Reset();
                return;
            }

            switch (mode & 0x7)
            {
                case 0:
                    CoreFreq = 12000000;
                    break;
                case 1:
                    CoreFreq = 20000;
                    break;
                case 2:
                    CoreFreq = XtalFreq;
                    break;
                case 3:
                    CoreFreq = ((mode & 0x40) != 0) ? XtalFreq * 1 : 0;
                    break;
                case 4:
                    CoreFreq = ((mode & 0x40) != 0) ? XtalFreq * 2 : 0;
                    break;
                case 5:
                    CoreFreq = ((mode & 0x40) != 0) ? XtalFreq * 4 : 0;
                    break;
                case 6:
                    CoreFreq = ((mode & 0x40) != 0) ? XtalFreq * 8 : 0;
                    break;
                case 7:
                    CoreFreq = ((mode & 0x40) != 0) ? XtalFreq * 16 : 0;
                    break;
            }

            for (int i = 0; i < Cogs.Length; i++)
                if (Cogs[i] != null)
                    Cogs[i].SetClock(CoreFreq);

            CoreClockSource.SetFrequency(CoreFreq);
        }

        /// @brief Reset the propeller CPU to initial state.
        /// 
        /// Release cog instances, clock sources, clear locks and pins, and reset plugins.
        /// @version 14.7.21 - Separate reset for clocksources, cogs and locks.
        public void Reset()
        {
            ResetMemory.CopyTo(Memory, 0);

            SystemCounter = 0;
            Time = 0;
            RingPosition = 0;
            for (int i = 0; i < ClockSources.Length; i++)   //clear clock source references
            {
                ClockSources[i] = null;
            }
            for (int i = 0; i < TOTAL_COGS; i++)    //clear cog references
            {
                Cogs[i] = null;
            }
            for (int i = 0; i < TOTAL_LOCKS; i++)    //clear locks state
            {
                LocksAvailable[i] = true;
                LocksState[i] = false;
            }

            foreach (PluginBase mod in PlugIns)
                mod.OnReset();

            PinChanged();   //update situation of pins

            SetClockMode((byte)(ClockMode & 0x7F));

            // Start the runtime in interpreted mode (fake boot)

            // Pushes the 3 primary offsets (local offset, var offset, and object offset)
            // Stack -1 is the boot parameter

            uint InitFrame = DirectReadWord(10);

            DirectWriteWord(InitFrame - 8, DirectReadWord(6));  // Object
            DirectWriteWord(InitFrame - 6, DirectReadWord(8));  // Var
            DirectWriteWord(InitFrame - 4, DirectReadWord(12)); // Local
            DirectWriteWord(InitFrame - 2, DirectReadWord(14)); // Stack

            // Boot parameter is Inital PC in the lo word, and the stack frame in the hi word
            ClockSources[0] = new PLLGroup();

            Cogs[0] = new InterpretedCog(this, InitFrame, CoreFreq, (PLLGroup)ClockSources[0]);
        }

        /// @brief Stop a %cog in the %propeller.
        ///
        /// @param[in] cog %Cog number to stop.
        public void Stop(int cog)
        {
            if (cog >= TOTAL_COGS || cog < 0)
                return;

            if (Cogs[cog] != null)
            {
                Cogs[cog].DetachVideoHooks();
                Cogs[cog] = null;
                ClockSources[cog] = null;
            }
        }

        /// @brief Advance one clock step.
        /// Inside it calls the OnClock() method for each plugin as clock advances. Also update the
        /// pins, by efect of calling each cog and source of clocks.
        public bool Step()
        {
            ulong pins;
            ulong dir;
            int sourceTicked;
            bool cogResult;
            bool result = true;

            do
            {
                double minimumTime = CoreClockSource.TimeUntilClock;
                sourceTicked = -1;

                // Preserve initial state of the pins
                pins = IN;
                dir = DIR;

                for (int i = 0; i < ClockSources.Length; i++)
                {
                    if (ClockSources[i] == null)
                        continue;

                    double clockTime = ClockSources[i].TimeUntilClock;
                    if (clockTime < minimumTime)
                    {
                        minimumTime = clockTime;
                        sourceTicked = i;
                    }
                }

                CoreClockSource.AdvanceClock(minimumTime);

                for (int i = 0; i < ClockSources.Length; i++)
                {
                    if (ClockSources[i] == null)
                        continue;

                    ClockSources[i].AdvanceClock(minimumTime);
                }

                Time += minimumTime; // Time increment

                if (sourceTicked != -1 && ((pins != IN || dir != DIR) || pinChange))
                    PinChanged();
            }
            while (sourceTicked != -1);

            // CPU advances on the main clock source
            RingPosition = (RingPosition + 1) & 0xF;    // 16 positions on the ring counter

            for (int i = 0; i < Cogs.Length; i++)
                if (Cogs[i] != null)
                {
                    cogResult = Cogs[i].Step();
                    result &= cogResult;
                }

            if ((RingPosition & 1) == 0)  // Every other clock, a cog gets a tick
            {
                uint cog = RingPosition >> 1;
                if (Cogs[cog] != null)
                    Cogs[cog].HubAccessable();
            }

            if (pins != IN || dir != DIR || pinChange)
                PinChanged();

            pins = IN;
            dir = DIR;

            // Advance the system counter
            SystemCounter++;

            // Run our modules on time event
            foreach (PluginBase mod in TickHandlers)
            {
                mod.OnClock(Time);
            }

            if (pins != IN || dir != DIR || pinChange)
                PinChanged();

            return result;
        }

        /// @brief Update pin information when are changes.
        /// Consider changes in DIRA and DIRB, and also generated in plugins.
        /// Inside it calls the OnPinChange() method for each plugin.
        public void PinChanged()
        {
            ulong pinsState = OUT;  //get total pins (P63..P0) OUT state

            pinChange = false;

            for (int i = 0; i < TOTAL_PINS; i++)    //loop for each pin of the chip
            {
                if (((DIR >> i) & 1) == 0)  //if Pin i has direction set to INPUT
                {
                    if (((PinFloat >> i) & 1) != 0)
                        PinStates[i] = PinState.FLOATING;
                    else if (((PinHi >> i) & 1) != 0)
                        PinStates[i] = PinState.INPUT_HI;
                    else
                        PinStates[i] = PinState.INPUT_LO;
                }
                else                     //then Pin i has direction set to OUTPUT
                {
                    if (((pinsState >> i) & 1) != 0)
                        PinStates[i] = PinState.OUTPUT_HI;
                    else
                        PinStates[i] = PinState.OUTPUT_LO;
                }
            }
            //traverse across plugins that use NotityOnPins()
            foreach (PluginBase mod in PinNoiseHandlers)
                mod.OnPinChange(Time, PinStates);
        }

        /// @todo Document method Gear.EmulationCore.PropellerCPU.DrivePin().
        /// 
        public void DrivePin(int pin, bool Floating, bool Hi)
        {
            ulong mask = (ulong)1 << pin;

            if (Floating)
                PinFloat |= mask;
            else
                PinFloat &= ~mask;

            if (Hi)
                PinHi |= mask;
            else
                PinHi &= ~mask;

            pinChange = true;
        }

        /// @todo Document method Gear.EmulationCore.PropellerCPU.LockSet().
        /// 
        public uint LockSet(uint number, bool set)
        {
            bool oldSetting = LocksState[number & 0x7];
            LocksState[number & 0x7] = set;
            return oldSetting ? 0xFFFFFFFF : 0;
        }

        /// @todo Document method Gear.EmulationCore.PropellerCPU.LockReturn().
        /// 
        public void LockReturn(uint number)
        {
            LocksAvailable[number & 0x7] = true;
        }

        /// @todo Document method Gear.EmulationCore.PropellerCPU.NewLock().
        /// 
        public uint NewLock()
        {
            for (uint i = 0; i < LocksAvailable.Length; i++)
                if (LocksAvailable[i])
                {
                    LocksAvailable[i] = false;
                    return i;
                }

            return 0xFFFFFFFF;
        }

        /// @todo Document method Gear.EmulationCore.PropellerCPU.CogID().
        /// 
        public uint CogID(Cog caller)
        {
            for (uint i = 0; i < Cogs.Length; i++)
                if (caller == Cogs[i])
                    return i;

            return 0;
        }

        /// @todo Document method Gear.EmulationCore.PropellerCPU.HubOp().
        /// 
        public uint HubOp(Cog caller, uint operation, uint arguement, ref bool carry)
        {
            switch ((HubOperationCodes)operation)
            {
                case HubOperationCodes.HUBOP_CLKSET:
                    SetClockMode((byte)arguement);
                    break;
                case HubOperationCodes.HUBOP_COGID:
                    {
                        // TODO: DETERMINE CARRY
                        return CogID(caller);
                    }
                case HubOperationCodes.HUBOP_COGINIT:
                    {
                        uint cog = (uint)Cogs.Length;
                        uint param = (arguement >> 16) & 0xFFFC;
                        uint progm = (arguement >> 2) & 0xFFFC;

                        // Start a new cog?
                        if ((arguement & TOTAL_COGS) != 0)
                        {
                            for (uint i = 0; i < Cogs.Length; i++)
                            {
                                if (Cogs[i] == null)
                                {
                                    cog = i;
                                    break;
                                }
                            }

                            if (cog >= Cogs.Length)
                            {
                                carry = true;
                                return 0xFFFFFFFF;
                            }
                        }
                        else
                        {
                            cog = (arguement & 7);
                        }

                        PLLGroup pll = new PLLGroup();

                        ClockSources[cog] = (ClockSource)pll;

                        if (progm == 0xF004)
                            Cogs[cog] = new InterpretedCog(this, param, CoreFreq, pll);
                        else
                            Cogs[cog] = new NativeCog(this, progm, param, CoreFreq, pll);

                        carry = false;
                        return (uint)cog;
                    }
                case HubOperationCodes.HUBOP_COGSTOP:
                    Stop((int)(arguement & 7));

                    // TODO: DETERMINE CARRY
                    // TODO: DETERMINE RESULT
                    return arguement;
                case HubOperationCodes.HUBOP_LOCKCLR:
                    carry = LocksState[arguement & 7];
                    LocksState[arguement & 7] = false;
                    // TODO: DETERMINE RESULT
                    return arguement;
                case HubOperationCodes.HUBOP_LOCKNEW:
                    for (uint i = 0; i < LocksAvailable.Length; i++)
                    {
                        if (LocksAvailable[i])
                        {
                            LocksAvailable[i] = false;
                            carry = false;
                            return i;
                        }
                    }
                    carry = true;   // No Locks available
                    return 0;       // Return 0 ?
                case HubOperationCodes.HUBOP_LOCKRET:
                    LocksAvailable[arguement & 7] = true;
                    // TODO: DETERMINE CARRY
                    // TODO: DETERMINE RESULT
                    return arguement;
                case HubOperationCodes.HUBOP_LOCKSET:
                    carry = LocksState[arguement & 7];
                    LocksState[arguement & 7] = true;
                    // TODO: DETERMINE RESULT
                    return arguement;
                default:
                    // TODO: RAISE EXCEPTION
                    break;
            }

            return 0;
        }

    }
}
