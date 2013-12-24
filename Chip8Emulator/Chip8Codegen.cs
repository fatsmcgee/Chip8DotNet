using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using Chip8Emulator;

namespace Chip8Emulator
{
    public enum Operator
    {
        CLS,
        RET,
        JP,
        CALL,
        SE,
        SNE,
        LD,
        ADD,
        OR,
        AND,
        XOR,
        SUB,
        SHR,
        SUBN,
        SHL,
        RND,
        DRW,
        SKP,
        SKNP
    }

    public class Argument
    {
        public static RegisterArgument[] Register { get; private set; }
        public static NibbleArgument[] Nibble { get; private set; }
        public static Argument I { get; private set; }
        public static Argument IndirectI { get; private set; }
   
        static Argument()
        {
            Register =
                Enumerable.Range(0, 16).Select(i => new RegisterArgument {Register = i}).ToArray();
            Nibble =
                Enumerable.Range(0, 16).Select(i => new NibbleArgument() {Nibble = i}).ToArray();
            I = new Argument();
            IndirectI = new Argument();
        }
    }
    

    public class RegisterArgument:Argument
    {
        private int register;

        public int Register
        {
            get { return register; }
            set
            {
                if (value < 0 || value > 15)
                    throw new ArgumentException("Register argument must be between 0 and 15");
                register = value;
            }
        }
    }

    public class NibbleArgument:Argument
    {
        private int value;
        public int Nibble
        {
            get { return value; }
            set
            {
                if (value < 0 || value > 15)
                    throw new ArgumentException("Register argument must be between 0 and 15");
                this.value = value;
            }
        }
    }

    public class AddressArgument:Argument
    {
        private int address;
        public int Address
        {
            get { return address; }
            set
            {
                if (value < 0 || value > 0xFFF)
                    throw new ArgumentException("Address argument must be between 0 and 0xFFF");
                address = value;
            }
        }
    }

    public class ByteArgument : Argument
    {
        private int value;
        public int Value
        {
            get { return value; }
            set
            {
                if (value < 0 || value > 255)
                    throw new ArgumentException("Address argument must be between 0 and 255");
                this.value = value;
            }
        }
    }

    public class IArgument : Argument
    {
    }
    public class IndirectIArgument:Argument
    {
        
    }


    public class Opcode
    {
        public Operator Operator { get; set; }
        public Argument[] Arguments { get; set; }
    }


    //just messing around
    internal class Chip8Codegen
    {
    }
}