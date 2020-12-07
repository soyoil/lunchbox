﻿using System;
using System.Collections.Generic;
using System.Text;

namespace Lunchbox
{
     public partial class Cpu
     {
        // 8bit registers
        private byte A { get; set; }
        private byte B { get; set; }
        private byte C { get; set; }
        private byte D { get; set; }
        private byte E { get; set; }
        private Flags F { get; set; }
        private byte H { get; set; }
        private byte L { get; set; }

        // 16bit registers
        private ushort SP { get; set; }
        internal ushort PC { get; set; }

        // Pair registers
        private ushort AF
        {
            get
            {
                return (ushort)(A * 0x100 + F);
            }
            set
            {
                A = (byte)(value / 0x100);
                F = (Flags)(value % 0x100);
            }
        }
        private ushort BC
        {
            get
            {
                return (ushort)(B * 0x100 + C);
            }
            set
            {
                B = (byte)(value / 0x100);
                C = (byte)(value % 0x100);
            }
        }
        private ushort DE
        {
            get
            {
                return (ushort)(D * 0x100 + E);
            }
            set
            {
                D = (byte)(value / 0x100);
                E = (byte)(value % 0x100);
            }
        }
        private ushort HL
        {
            get
            {
                return (ushort)(H * 0x100 + L);
            }
            set
            {
                H = (byte)(value / 0x100);
                L = (byte)(value % 0x100);
            }
        }

        internal Dictionary<string, ushort> RegDict
        {
            get
            {
                var dict = new Dictionary<string, ushort>
                {
                    { "A", A },
                    { "B", B },
                    { "C", C },
                    { "D", D },
                    { "E", E },
                    { "H", H },
                    { "L", L }
                };
                return dict;
            }
        }

        // Flags set
        [Flags]
        private enum Flags
        {
            Z = 1 << 7,
            N = 1 << 6,
            H = 1 << 5,
            C = 1 << 4,
        };

        private Action[] ops;

        private readonly Memory memory;

        // Constructor
        public Cpu(Memory memoryPtr = null)
        {
            A = B = C = D = E = H = L = 0;
            F = 0;
            PC = 0;
            memory = memoryPtr;
            ops = new Action[0xFF];
            RegisterOps();
        }

        private void SetFlag(Flags flag, bool isSet)
        {
            if (isSet)
            {
                F |= flag;
            }
            else
            {
                F &= ~flag;
            }
        }

        private bool GetFlag(Flags flag)
        {
            return F.HasFlag(flag);
        }

        public void run()
        {
            byte opcode = memory.Ram[PC];
            ops[opcode]();
            PC++;
        }

        internal void TestRun(ushort endAddr)
        {
            do
            {
                byte opcode = memory.Ram[PC];
                ops[opcode]();
            } while (PC++ != endAddr);
        }

        private ushort GetTwoBitesFromRam()
        {
            return (ushort)(memory.Ram[++PC] + memory.Ram[++PC] * 0x100);
        }

        private void Push(ushort value)
        {
            memory.Ram[--SP] = (byte)(value >> 4);
            memory.Ram[--SP] = (byte)(value & 0xFF);
        }

        private ushort Pop()
        {
            return (ushort)(memory.Ram[++SP] + (memory.Ram[SP] << 4));
        }

        private void Add(byte value, bool isADC = false)
        {
            var result = A + value + Convert.ToInt32(isADC);
            SetFlag(Flags.Z, result == 0);
            SetFlag(Flags.N, false);
            SetFlag(Flags.H, (A & 0xF) + (value & 0xF) + Convert.ToInt32(isADC) > 0xF);
            SetFlag(Flags.C, result > 0xFF);
            A = (byte)result;
        }

        private void Add(ushort value)
        {
            var result = HL + value;
            SetFlag(Flags.N, false);
            SetFlag(Flags.H, (HL & 0xF) + (value & 0xF) > 0xF);
            SetFlag(Flags.C, result > 0xFFFF);
            HL = (ushort)result;
        }

        private ushort AddSP()
        {
            var e = memory.Ram[++PC];
            var result = SP + e;
            SetFlag(Flags.Z, false);
            SetFlag(Flags.N, false);
            SetFlag(Flags.H, (HL & 0xF) + (e & 0xF) > 0xF);
            SetFlag(Flags.C, result > 0xFFFF);
            return (ushort)result;
        }

        private void Sub(byte value, bool isSBC = false)
        {
            A = Cp(value, isSBC);
        }

        private byte Cp(byte value, bool isSBC = false)
        {
            var result = A - value - Convert.ToInt32(isSBC);
            SetFlag(Flags.Z, result == 0);
            SetFlag(Flags.N, true);
            SetFlag(Flags.H, (A & 0xF) + (value & 0xF) + Convert.ToInt32(isSBC) < 0);
            SetFlag(Flags.C, result < 0);
            return (byte)result;
        }

        private void And(byte value)
        {
            A = (byte)(A & value);
            SetFlag(Flags.Z, A != 0);
            SetFlag(Flags.N, false);
            SetFlag(Flags.H, true);
            SetFlag(Flags.C, false);
        }

        private void Or(byte value)
        {
            A = (byte)(A | value);
            SetFlag(Flags.Z, A != 0);
            SetFlag(Flags.N, false);
            SetFlag(Flags.H, false);
            SetFlag(Flags.C, false);
        }

        private void Xor(byte value)
        {
            A = (byte)(A ^ value);
            SetFlag(Flags.Z, A != 0);
            SetFlag(Flags.N, false);
            SetFlag(Flags.H, false);
            SetFlag(Flags.C, false);
        }

        private byte Increment(byte value)
        {
            SetFlag(Flags.Z, value == 0xFF);
            SetFlag(Flags.N, false);
            SetFlag(Flags.H, (value & 0xF) == 0xF);
            return (byte)(value + 1);
        }

        private byte Decrement(byte value)
        {
            SetFlag(Flags.Z, value == 0);
            SetFlag(Flags.N, true);
            SetFlag(Flags.H, (value & 0xF) == 0);
            return (byte)(value - 1);
        }

        private void Daa()
        {
            int a = A;
            int collection = 0;
            Flags setFlagC = 0;
            if (GetFlag(Flags.H) || (!GetFlag(Flags.N) && (a & 0xF) > 9))
                collection |= 0x6;
            if(GetFlag(Flags.C) || (!GetFlag(Flags.N) && a > 0x99))
            {
                collection |= 0x60;
                setFlagC = Flags.C;
            }
            a += GetFlag(Flags.N) ? -collection : collection;
            a &= 0xFF;
            Flags setFlagZ = a == 0 ? Flags.Z : 0;
            F &= ~(Flags.Z | Flags.H | Flags.C);
            F |= setFlagC | setFlagZ;
            A = (byte)a;
        }
    }
}
