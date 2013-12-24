using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Chip8Emulator
{
    public class Chip8
    {
        //memory going from location 200h to FFFh
        //EA0 to EFF for call stack and internal use
        //F00 to FFF for display refresh
        private byte[] memory = new byte[0xFFF];

        //16 8 bit registers
        private byte[] registers = new byte[16];

        //16 keys
        private bool[] keys = new bool[16];

        //address stack of  some sort
        private Stack<UInt16> subroutineStack = new Stack<UInt16>(); 

        //program counter
        private UInt16 PC;

        //I, the address register
        private UInt16 I;

        //carry flag
        private bool VF {
            set { registers[15] = value ? (byte)1 : (byte)0; }
        }

        private Random random = new Random();

        //two timers that count down at 60 hertz until they reach 0
            //delay timer -- intended for timing of events in games, can be set and read
            //sound timer -- used for sound effets. When nonzero, a beeping sound is made

        //delay timer
        private byte DT;

        //sound timer
        private byte ST;

        //private List<string> codeLines = new List<string>(); 


        //keyboard -- keys ranging from 0 to F 9hex. 8,4,6,2 often used for input

        //display - 64x32 pixels, monochrome
            //all drawing is sprites, 8 pixels wide and 1 to 15 pixels high
            //set pixels flip the color on the screen, unset pixels do nothing
            //VF (carry flag) set to 1 if any pixel flipped from set to unset, and 0 otherwise

        private static readonly int ROWS = 32;
        private static readonly int COLS = 64;
        private bool[] displayBuf = new bool[ROWS*COLS];

        private void DisplaySprite(UInt16 spriteAddr, int nRows, int xLeft, int yTop)
        {
            VF = false;

            for (int row = 0; row < nRows; row++)
            {
                byte sprRow = memory[spriteAddr + row];
                for (int col = 0; col < 8; col++)
                {
                    //get pixel for row/col
                    bool sprPixel = ((sprRow >> (7 - col)) & 1) == 1;

                    if (sprPixel)
                    {
                        //copy to displayBuf at correct location. new pixel = old pixel ^ sprite pixel
                        int newX = (xLeft + col)%COLS;
                        int newY = (yTop + row)%ROWS;
                        bool oldPixel = displayBuf[newY*COLS + newX];

                        displayBuf[newY*COLS + newX] = !oldPixel; // newPixel;

                        if (oldPixel) // && !newPixel)
                            VF = true;
                    }

                }
            }
           
        }

        public Bitmap CreateBitmap()
        {
            //4 bytes per color
            Bitmap b = new Bitmap(COLS, ROWS, PixelFormat.Format24bppRgb);
            return b;
            
        }
        public void CopyToBitmap(Bitmap bitmap)
        {
            var data = bitmap.LockBits(new Rectangle(0, 0, COLS, ROWS), ImageLockMode.ReadWrite,
                                       bitmap.PixelFormat);
            

           
            var bytes = new byte[data.Stride*ROWS];

            for (int row = 0; row < ROWS; row ++)
            {
                for (int col = 0; col < COLS; col++)
                {
                    var value = (byte) (displayBuf[row*COLS + col] ? 0xFF : 0x00);
                    var offset = data.Stride*row + col*3;
                    bytes[offset] = value;
                    bytes[offset + 1] = value;
                    bytes[offset + 2] = value;
                }
            }

            //copy our bytes into the image
            Marshal.Copy(bytes, 0, data.Scan0, bytes.Length);

            bitmap.UnlockBits(data);
        }

        private UInt16 GetHexidecimalSpriteAddr(byte b)
        {
            return (UInt16)(b*5);

        }

        public void InitializeHexidecimalSprites()
        {
            var numbers = new List<byte>();
            //zero
            numbers.AddRange(new byte[] { 0xF0, 0x90, 0x90, 0x90, 0xF0 });
            //one
            numbers.AddRange(new byte[] {0x20, 0x60, 0x20, 0x20, 0x70});
            //two
            numbers.AddRange(new byte[] {0xF0, 0x10, 0xF0, 0x80, 0xF0});
            //three
            numbers.AddRange(new byte[] {0xF0, 0x10, 0xF0, 0x10, 0xF0});
            //four
            numbers.AddRange(new byte[] {0x90, 0x90, 0xF0, 0x10, 0x10});
            //five 
            numbers.AddRange(new byte[] {0xF0, 0x80, 0xF0, 0x10, 0xF0});
            //six
            numbers.AddRange(new byte[] { 0xF0, 0x80, 0xF0, 0x90, 0xF0 });
            //seven
            numbers.AddRange(new byte[] { 0xF0, 0x10, 0x20, 0x40, 0x40 });
            //eight
            numbers.AddRange(new byte[] { 0xF0, 0x90, 0xF0, 0x90, 0xF0 });
            //nine
            numbers.AddRange(new byte[] { 0xF0, 0x90, 0xF0, 0x10, 0xF0 });
            //ten
            numbers.AddRange(new byte[] { 0xF0, 0x90, 0xF0, 0x90, 0x90 });
            //eleven
            numbers.AddRange(new byte[] { 0xE0, 0x90, 0xE0, 0x90, 0xE0 });
            //twelve
            numbers.AddRange(new byte[] { 0xF0, 0x80, 0x80, 0x80, 0xF0 });
            //thirteen
            numbers.AddRange(new byte[] { 0xE0, 0x90, 0x90, 0x90, 0xE0 });
            //fourteen
            numbers.AddRange(new byte[] { 0xF0, 0x80, 0xF0, 0x80, 0xF0 });
            //fifteen
            numbers.AddRange(new byte[] { 0xF0, 0x80, 0xF0, 0x80, 0x80 });
            for (var i = 0; i < numbers.Count; i++)
            {
                memory[i] = numbers[i];
            }
        }

        private static UInt16 GetAddr(int nibble1, int nibble2, int nibble3)
        {
            return (UInt16) (nibble3 + (nibble2 << 4) + (nibble1 << 8));
        }

        public void Step()
        {
            
            var opcode = (UInt16) ((memory[PC] << 8) + memory[PC + 1]);

            //string opAddr = String.Format("{0:X} -- {1:X}", PC - 0x200, opcode);

           /* try
            {
                string codeLine = codeLines[(PC - 0x200)/2];
                codeLine+=String.Join(",", Enumerable.Range(0, 16).Select(i => "V" + i + "=" + registers[i]));
                codeLine += ",I=" + I.ToString("X");
                File.AppendAllLines("C:\\log.txt", new List<string> {codeLine});
            }
            catch 
            {
            }*/
            HandleOpcode(opcode);
            PC += 2;

            if (DT != 0)
                DT--;
            if (ST != 0)
                ST--;

        }

        //35 opcodes, each two bytes long. Most significant byte first.
        public void HandleOpcode(UInt16 opcode)
        {
            //eg 0xDEAF, D = nibble 1 ..., F = nibble4
            int nibble1 = opcode >> 12;
            int nibble2 = (opcode >> 8) & 0x0F;
            int nibble3 = (opcode >> 4) & 0x0F;
            int nibble4 = opcode & 0x0F;

            byte byte2 = (byte) (opcode & 0xFF);
            UInt16 addrArg = GetAddr(nibble2, nibble3, nibble4);

            if (nibble1 == 0)
            {
                if (byte2 == 0xE0)
                {
                    // 00E0, clear the display
                    for (int i = 0; i < displayBuf.Length; i++)
                        displayBuf[i] = false;
                }
                else if (byte2 == 0xEE)
                {
                   //00EE, return from subroutine
                    PC = subroutineStack.Pop();
                }
            }
            else if (nibble1 == 1)
            {
                //1nnn, jump
                PC = (UInt16) (addrArg - 2);
            }
            else if (nibble1 == 2)
            {
                //2nnn, call subroutine at addr
                subroutineStack.Push(PC);
                PC = (UInt16) (addrArg - 2);
            }
            else if (nibble1 == 3)
            {
                //3xkk, skip next instruction if V[x] == kk
                if (registers[nibble2] == byte2)
                {
                    PC += 2;
                }

            }
            else if (nibble1 == 4)
            {
                //4xkk skip next instruction if V[x] != kk
                if (registers[nibble2] != byte2)
                {
                    PC += 2;
                }
            }
            else if (nibble1 == 5)
            {
                if (nibble4 == 0)
                {
                    //5xy0, skip next instruction if V[x] == V[y]
                    if (registers[nibble2] == registers[nibble3])
                    {
                        PC += 2;
                    }
                }
            }
            else if (nibble1 == 6)
            {
                //6xkk, set V[x] to kk
                registers[nibble2] = byte2;
            }
            else if (nibble1 == 7)
            {
                //7xkk, V[x] += kk
                registers[nibble2] += byte2;
            }
            else if (nibble1 == 8)
            {
                switch (nibble4)
                {
                    case 0:
                        //8xy0, set V[x] = V[y]
                        registers[nibble2] = registers[nibble3];
                        break;
                    case 1:
                        //8xy1 V[x] = V[x] | V[y] 
                        registers[nibble2] |= registers[nibble3];
                        break;
                    case 2:
                        //8xy2, V[x] = V[x] & V[y]
                        registers[nibble2] &= registers[nibble3];
                        break;
                    case 3:
                        //8xy3, V[x] ^= V[y]
                        registers[nibble2] ^= registers[nibble3];
                        break;
                    case 4:
                        //8xy4, V[x] += V[y], set carry appropriately
                        if (registers[nibble2] + registers[nibble3] > 255)
                            VF = true;
                        registers[nibble2] += registers[nibble3];
                        break;
                    case 5:
                        //8xy5, V[x] -= V[y], set carry if there is NO borrowing
                        if (registers[nibble2] > registers[nibble3])
                            VF = true;
                        else
                        {
                            VF = false;
                        }
                        registers[nibble2] -= registers[nibble3];
                        break;
                    case 6:
                        //8xy6, shift V[x] right by 1, VF set to least significant bit of V[x] before shift
                        VF = (registers[nibble2] & 1) == 1;
                        registers[nibble2] >>= 1;
                        break;
                    case 7:
                        //8xy7, V[x] = V[y] - V[x], set VF if V[y]>V[x]
                        if (registers[nibble3] > registers[nibble2])
                            VF = true;
                        registers[nibble2] = (byte) (registers[nibble3] - registers[nibble2]);
                        break;
                    case 0xE:
                        //8xyE, set V[x] = V[x] << 1, VF set to most significant bit of V[x] before shift
                        VF = (registers[nibble2] >> 15) == 1;
                        registers[nibble2] <<= 1;
                        break;

                }
            }

            else if(nibble1 == 9 )
            {
                 if (nibble4 == 0)
                 {
                     //9xy0, skip next instruction if V[x] != V[y]
                     if (registers[nibble2] != registers[nibble3])
                     {
                         PC += 2;
                     }
                 }
            }
            else if (nibble1 == 0xA)
            {
                //Annn, set I to nnn
                I = addrArg;
            }
            else if (nibble1 == 0xB)
            {
                //Bnnn, jump to location nnn+ V[0]
                PC = (UInt16) (addrArg + registers[0]);
            }
            else if (nibble1 == 0xC)
            {
                //cxkk, set V[x] = random byte & kk
                registers[nibble2] = (byte) (random.Next() & byte2);
            }
            else if (nibble1 == 0xD)
            {
                //Dxyn display n-byte sprite starting at memory location I at (V[x], V[y]), set VF = collision
                DisplaySprite(I,nibble4,registers[nibble2],registers[nibble3]);
            }
            else if (nibble1 == 0xE)
            {
                if (byte2 == 0x9E)
                {
                    //Ex9E, skip next instruction if keys[x] is pressed
                    if (keys[registers[nibble2]])
                    {
                        PC += 2;
                    }
                }
                else if (byte2 == 0xA1)
                {
                    //ExA1, skip next instruction if keys[0] is not pressed
                    if (!keys[registers[nibble2]])
                    {
                        PC += 2;
                    }
                }
            }
            else if (nibble1 == 0xF)
            {
                switch (byte2)
                {
                    case 0x07:
                        //Fx07, set V[x] to delay timer
                        registers[nibble2] = DT;
                        break;
                    case 0x15:
                        //Fx15, set delay timer to V[x]
                        DT = registers[nibble2];
                        break;
                    case 0x18:
                        //Fx18, set sound timer to V[x]
                        ST = registers[nibble2];
                        break;
                    case 0x1E:
                        //Fx1E, I+=V[x]
                        I += registers[nibble2];
                        break;
                    case 0x29:
                        //Fx29, set I = location of sprite for digit V[x]
                        I = GetHexidecimalSpriteAddr(registers[nibble2]);
                        break;
                    case 0x33:
                        //Fx33, store BCD rep. of V[x] in I,I+1,and I+2
                        //I = hundreds, I+1 = tens, I+2 = digit
                        var value = registers[nibble2];
                        memory[I] = (byte) (value/100);
                        memory[I + 1] = (byte) ((value/10)%10);
                        memory[I + 2] = (byte) (value%10);
                        break;
                    case 0x55:
                        //Fx66, store registers V[0] to V[x] in memory starting at location I
                        for (int i = 0; i < nibble2 + 1; i++)
                        {
                            memory[I + i] = registers[i];
                        }
                        break;
                    case 0x65:
                        //Fx65, read registers V[0] to V[x] from memory starting at I
                        for (int i = 0; i < nibble2+1; i++)
                        {
                            registers[i] = memory[I + i];
                        }
                        break;
                }
            }
            
        }

        /*
        public Chip8 ()
        {
            codeLines =
                File.ReadLines("E:\\Downloads\\BRIX.SRC")
                    .Where(l => !String.IsNullOrEmpty(l) && !l.Contains(":"))
                    .ToList();

        }*/

        public void Reset()
        {
            PC = 0x200;
            //zero out memory and display, and set number spirtes
            for (int i = 0; i < memory.Length; i++)
            {
                memory[i] = 0;
            }

            for (int i = 0; i < 16; i++)
            {
                registers[i] = 0;
                keys[i] = false;
            }

            for (int i = 0; i < displayBuf.Length; i++)
            {
                displayBuf[i] = false;
            }

            InitializeHexidecimalSprites();

        }

        public void LoadRomFromPath(string file)
        {
            Reset();
            var bytes = File.ReadAllBytes(file);
            for (int i = 0; i < bytes.Length; i++)
            {
                //copy memory from ROM starting at 0x200
                memory[0x200 + i] = bytes[i];
            }
            
        }

        public void SetKeyDown(int value)
        {
            keys[value] = true;
        }

        public void SetKeyUp(int value)
        {
            keys[value] = false;
        }
    }
}
