using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace HuffmanTextFileCompressor
{
    public partial class MainForm : Form
    {
        private int iCount, leafNodes;
        private List<CharFreq> charFreq;
        public struct Header
        {
            public int inputFileLength;    // # ASCII input characters
            public int tableLength;        // # of codes and chars
            public int bitLength;          // # of bits per character
            public int outputDataLength;   // # of output bytes
            public byte[] chars;           // the characters in byte form
            // probably should add hash value of input file but
            // this is a public domain program so we can't export
            // a cryptographically sound and seecure hash function
        }
        private Header header;

        public MainForm()
        {
            InitializeComponent();
        }

        public int GetBit(int i, byte[] buffer)
        {
            int b = i % 8;
            int c = i / 8;

            return ((int)buffer[c] >> (7 - b)) & 1;
        }

        private void SetBit(int i, int v, byte[] buffer)
        {
            int b = i % 8;
            int c = i / 8;
            int mask = 1 << (7 - b);
            int iBlock = (int)buffer[c];

            if (v == 1)
                iBlock |= mask;
            else
                iBlock &= ~mask;

            buffer[c] = (byte)iBlock;
        }

        private void InorderTraversal(BinaryTreeNode<CharFreq> node)
        {
            if (node != null)
            {
                InorderTraversal(node.Left);

                CharFreq cf = node.Value;
                int ord = (int)cf.ch;

                if (node.Left == null && node.Right == null)
                {
                    leafNodes++;
                    textBox1.Text += ord.ToString("D3") + "\t";
                    textBox1.Text += cf.freq.ToString() + "\r\n";
                }

                InorderTraversal(node.Right);
            }
        }

        private void Compress()
        {
            int bitLength = 0, block = 0, bpBlock = 0;
            HuffmanTree ht = new HuffmanTree();
            BinaryTreeNode<CharFreq> root = ht.Build(charFreq, charFreq.Count);

            leafNodes = 0;
            textBox1.Text += "The Huffman tree inorder traversal\r\n\r\n";
            InorderTraversal(root);

            if (leafNodes != charFreq.Count)
            {
                MessageBox.Show("Compression tree build error", "Warning",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            textBox1.Text += "\r\nTotal number of input characters = "
                + iCount.ToString() + "\r\n";
            textBox1.Text += "Total number of leaf characters = "
                + leafNodes.ToString() + "\r\n";

            if (leafNodes < 2)
            {
                bitLength = 1;
                block = 8;
                bpBlock = 1;
            }
            else if (leafNodes < 4)
            {
                bitLength = 2;
                block = 4;
                bpBlock = 1;
            }
            else if (leafNodes < 8)
            {
                bitLength = 3;
                block = 8;
                bpBlock = 3;
            }
            else if (leafNodes < 16)
            {
                bitLength = 4;
                block = 2;
                bpBlock = 1;
            }
            else if (leafNodes < 32)
            {
                bitLength = 5;
                block = 8;
                bpBlock = 5;
            }
            else if (leafNodes < 64)
            {
                bitLength = 6;
                block = 4;
                bpBlock = 3;
            }
            else if (leafNodes < 128)
            {
                bitLength = 7;
                block = 8;
                bpBlock = 8;
            }

            int number =  (leafNodes * bitLength) / block;
            int numberBits = bitLength * iCount;
            int numberBlocks = numberBits / (8 * bpBlock);
            int remainder = numberBits % (8 * bpBlock);

            header = new Header();
            header.inputFileLength = iCount;
            header.outputDataLength = numberBlocks * bpBlock + remainder / bitLength;
            header.tableLength = leafNodes;
            header.bitLength = bitLength;
            header.chars = new byte[leafNodes];

            textBox1.Text += "Total data length in bytes = "
                + header.outputDataLength.ToString() + "\r\n";
            textBox1.Text += "Total bit length = "
                + bitLength.ToString() + "\r\n";

            int headerLength = 4 * sizeof(int) + leafNodes * sizeof(byte);
            int outputFileLength = headerLength + header.outputDataLength;
            double percent = 100.0 - 100.0 * ((double)outputFileLength) / iCount;

            textBox1.Text += "Total length of compressed file in bytes = "
               + outputFileLength.ToString() + "\r\n";
            textBox1.Text += "% Compression = "
                + percent.ToString("F2") + "\r\n";

            if (percent <= 0.0)
            {
                MessageBox.Show("% Compression is negative or zero", "Warning",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            for (int i = 0; i < charFreq.Count; i++)
                header.chars[i] = (byte)charFreq[i].ch;

            try
            {
                if (saveFileDialog1.ShowDialog() == DialogResult.OK)
                {
                    FileStream fs = new FileStream(saveFileDialog1.FileName,
                        FileMode.Create);
                    BinaryWriter bw = new BinaryWriter(fs);
                    StreamReader sr = new StreamReader(openFileDialog1.FileName);
                    byte[] ibuff = new byte[block];
                    byte[] codes = new byte[block];

                    bw.Write(header.inputFileLength);
                    bw.Write(header.tableLength);
                    bw.Write(header.bitLength);
                    bw.Write(header.outputDataLength);
                    bw.Write(header.chars, 0, header.tableLength);

                    for (int k = 0; k < header.inputFileLength / block; k++)
                    {
                        for (int i = 0; i < block; i++)
                        {
                            ibuff[i] = (byte)sr.Read();
                            codes[i] = (byte)255;

                            for (int j = 0; j < header.tableLength; j++)
                            {
                                if (ibuff[i] == header.chars[j])
                                {
                                    codes[i] = (byte)j;
                                    break;
                                }
                            }

                            if (codes[i] == (byte)255)
                            {
                                MessageBox.Show("Code not found in lookup", "Warning",
                                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                                return;
                            }
                        }

                        byte[] obuff = new byte[bpBlock];

                        for (int i = 0; i < block; i++)
                        {
                            int c = (int)codes[i];

                            for (int j = 0; j < bitLength; j++)
                            {
                                int bit = (c >> (bitLength - j - 1)) & 1;

                                SetBit(i * bitLength + j, bit, obuff);
                            }
                        }

                        bw.Write(obuff, 0, bpBlock);
                    }

                    if (header.inputFileLength % block != 0)
                    {
                        for (int i = 0; i < header.inputFileLength % block; i++)
                        {
                            byte ch = (byte)sr.Read();

                            bw.Write(ch);
                        }
                    }

                    bw.Flush();
                    sr.Close();
                    bw.Close();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), "Warning",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void button1_Click(object sender, EventArgs e)
        {
            try
            {
                saveFileDialog1.Filter = "Huffman Files (*.huf)|*.huf";
                openFileDialog1.Filter = "Text Files (*.txt)|*.txt";

                if (openFileDialog1.ShowDialog() == DialogResult.OK)
                {
                    int[] freq = new int[128];
                    StreamReader sr = new StreamReader(openFileDialog1.FileName);

                    iCount = 0;

                    while (!sr.EndOfStream)
                    {
                        int ch = sr.Read();

                        if (ch >= 0 && ch <= 127)
                            freq[ch]++;
                        else
                        {
                            MessageBox.Show("File is not ASCII", "Warning",
                                MessageBoxButtons.OK, MessageBoxIcon.Warning);
                            return;
                        }

                        iCount++;
                    }

                    sr.Close();
                    charFreq = new List<CharFreq>(128);

                    for (int i = 0; i < 128; i++)
                    {
                        if (freq[i] != 0)
                        {
                            CharFreq cf = new CharFreq();

                            cf.ch = (char)i;
                            cf.freq = freq[i];
                            charFreq.Add(cf);
                        }
                    }

                    Compress();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), "Warning",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void button2_Click(object sender, EventArgs e)
        {
            try
            {
                openFileDialog1.Filter = "Huffman Files (*.huf)|*.huf";
                saveFileDialog1.Filter = "Text Files (*.txt)|*.txt";

                if (openFileDialog1.ShowDialog() == DialogResult.OK)
                {
                    if (saveFileDialog1.ShowDialog() == DialogResult.OK)
                    {
                        FileStream fs = new FileStream(openFileDialog1.FileName,
                            FileMode.Open);
                        BinaryReader br = new BinaryReader(fs);
                        StreamWriter sw = new StreamWriter(saveFileDialog1.FileName);
                        int inputFileLength = br.ReadInt32(), block = 0, bpBlock = 0;
                        int tableLength = br.ReadInt32(), totalWritten = 0;
                        int bitLength = br.ReadInt32();
                        int outputDataLength = br.ReadInt32();
                        byte[] chars = br.ReadBytes(tableLength);

                        textBox1.Text = string.Empty;
                        textBox1.Text += "Input file length = "
                            + inputFileLength.ToString() + "\r\n";
                        textBox1.Text += "Table length = "
                            + tableLength.ToString() + "\r\n";
                        textBox1.Text += "Bit length = "
                            + bitLength.ToString() + "\r\n";
                        textBox1.Text += "Output data length = "
                            + outputDataLength.ToString() + "\r\n";

                        if (bitLength == 1)
                        {
                            block = 8;
                            bpBlock = 1;
                        }
                        else if (bitLength == 2)
                        {
                            block = 4;
                            bpBlock = 1;
                        }
                        else if (bitLength == 3)
                        {
                            block = 8;
                            bpBlock = 3;
                        }
                        else if (bitLength == 4)
                        {
                            block = 2;
                            bpBlock = 1;
                        }
                        else if (bitLength == 5)
                        {
                            block = 8;
                            bpBlock = 5;
                        }
                        else if (bitLength == 6)
                        {
                            block = 4;
                            bpBlock = 3;
                        }
                        else if (bitLength == 7)
                        {
                            block = 8;
                            bpBlock = 8;
                        }

                        for (int i = 0; i < inputFileLength / block; i++)
                        {
                            byte[] ibuff = br.ReadBytes(bpBlock);

                            for (int j = 0; j < block; j++)
                            {
                                int code = 0;

                                for (int k = 0; k < bitLength; k++)
                                {
                                    int bit = GetBit(j * bitLength + k, ibuff);

                                    code |= bit << (bitLength - k - 1);
                                }

                                char ch = (char)chars[code];

                                sw.Write(ch);
                                totalWritten++;
                            }
                        }

                        if (inputFileLength % block != 0)
                        {
                            for (int i = 0; i < inputFileLength % block; i++)
                            {
                                byte ch = br.ReadByte();

                                sw.Write(ch);
                                totalWritten++;
                            }
                        }

                        sw.Flush();
                        br.Close();
                        sw.Close();

                        textBox1.Text += "Decompressed file length = "
                            + totalWritten + "\r\n";

                        if (totalWritten != inputFileLength)
                        {
                            MessageBox.Show("Houston we have a problem", "Warning",
                                MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), "Warning",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void button3_Click(object sender, EventArgs e)
        {
            textBox1.Text = string.Empty;
        }
    }
}
