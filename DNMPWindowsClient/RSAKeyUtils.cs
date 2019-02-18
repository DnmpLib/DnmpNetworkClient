/*
Copyright (c) 2000  JavaScience Consulting,  Michel Gallant
 
Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:
 
The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.
 
THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE.
*/

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;

namespace DNMPWindowsClient
{
    class RSAKeyUtils
    {

        #region PUBLIC KEY TO X509 BLOB
        internal static byte[] PublicKeyToX509(RSAParameters publicKey)
        {
            AsnType oid = CreateOid("1.2.840.113549.1.1.1");
            AsnType algorithmID =
              CreateSequence(new AsnType[] { oid, CreateNull() });

            AsnType n = CreateIntegerPos(publicKey.Modulus);
            AsnType e = CreateIntegerPos(publicKey.Exponent);
            AsnType key = CreateBitString(
              CreateSequence(new AsnType[] { n, e })
            );

            AsnType publicKeyInfo =
              CreateSequence(new AsnType[] { algorithmID, key });

            return new AsnMessage(publicKeyInfo.GetBytes(), "X.509").GetBytes();
        }
        #endregion BLOB

        #region PRIVATE KEY TO PKCS8 BLOB
        internal static byte[] PrivateKeyToPKCS8(RSAParameters privateKey)
        {
            AsnType n = CreateIntegerPos(privateKey.Modulus);
            AsnType e = CreateIntegerPos(privateKey.Exponent);
            AsnType d = CreateIntegerPos(privateKey.D);
            AsnType p = CreateIntegerPos(privateKey.P);
            AsnType q = CreateIntegerPos(privateKey.Q);
            AsnType dp = CreateIntegerPos(privateKey.DP);
            AsnType dq = CreateIntegerPos(privateKey.DQ);
            AsnType iq = CreateIntegerPos(privateKey.InverseQ);
            AsnType version = CreateInteger(new byte[] { 0 });
            AsnType key = CreateOctetString(
              CreateSequence(new AsnType[] { version, n, e, d, p, q, dp, dq, iq })
            );

            AsnType algorithmID = CreateSequence(new AsnType[] { CreateOid("1.2.840.113549.1.1.1"), CreateNull() });

            AsnType privateKeyInfo = CreateSequence(new AsnType[] { version, algorithmID, key });
            return new AsnMessage(privateKeyInfo.GetBytes(), "PKCS#8").GetBytes();
        }

        internal static byte[] PrivateKeyToPKCS8(byte[] privkey)
        {
            RSAParameters RSAParam = DecodeRSAPrivateKeyToRSAParam(privkey);
            AsnType n = CreateIntegerPos(RSAParam.Modulus);
            AsnType e = CreateIntegerPos(RSAParam.Exponent);
            AsnType d = CreateIntegerPos(RSAParam.D);
            AsnType p = CreateIntegerPos(RSAParam.P);
            AsnType q = CreateIntegerPos(RSAParam.Q);
            AsnType dp = CreateIntegerPos(RSAParam.DP);
            AsnType dq = CreateIntegerPos(RSAParam.DQ);
            AsnType iq = CreateIntegerPos(RSAParam.InverseQ);
            AsnType version = CreateInteger(new byte[] { 0 });
            AsnType key = CreateOctetString(
              CreateSequence(new AsnType[] { version, n, e, d, p, q, dp, dq, iq })
            );

            AsnType algorithmID = CreateSequence(new AsnType[] { CreateOid("1.2.840.113549.1.1.1"), CreateNull() });

            AsnType privateKeyInfo = CreateSequence(new AsnType[] { version, algorithmID, key });
            return new AsnMessage(privateKeyInfo.GetBytes(), "PKCS#8").GetBytes();
        }
        #endregion

        #region X509 PUBLIC KEY BLOB TO RSACRYPTOPROVIDER
        internal static RSACryptoServiceProvider DecodePublicKey(byte[] publicKeyBytes)
        {
            MemoryStream ms = new MemoryStream(publicKeyBytes);
            BinaryReader rd = new BinaryReader(ms);
            byte[] SeqOID = { 0x30, 0x0D, 0x06, 0x09, 0x2A, 0x86, 0x48, 0x86, 0xF7, 0x0D, 0x01, 0x01, 0x01, 0x05, 0x00 };
            byte[] seq = new byte[15];

            try
            {
                byte byteValue;
                ushort shortValue;

                shortValue = rd.ReadUInt16();

                switch (shortValue)
                {
                    case 0x8130:
                        rd.ReadByte(); break;
                    case 0x8230:
                        rd.ReadInt16(); break;
                    default:
                        return null;
                }

                seq = rd.ReadBytes(15); if (!Helpers.CompareBytearrays(seq, SeqOID)) return null;

                shortValue = rd.ReadUInt16();
                if (shortValue == 0x8103) rd.ReadByte();
                else if (shortValue == 0x8203)
                    rd.ReadInt16();
                else
                    return null;

                byteValue = rd.ReadByte();
                if (byteValue != 0x00)
                    return null;

                shortValue = rd.ReadUInt16();
                if (shortValue == 0x8130) rd.ReadByte();
                else if (shortValue == 0x8230)
                    rd.ReadInt16();
                else
                    return null;


                CspParameters parms = new CspParameters();
                parms.Flags = CspProviderFlags.NoFlags;
                parms.KeyContainerName = Guid.NewGuid().ToString().ToUpperInvariant();
                parms.ProviderType = ((Environment.OSVersion.Version.Major > 5) || ((Environment.OSVersion.Version.Major == 5) && (Environment.OSVersion.Version.Minor >= 1))) ? 0x18 : 1;

                RSACryptoServiceProvider rsa = new RSACryptoServiceProvider(parms);
                RSAParameters rsAparams = new RSAParameters();

                rsAparams.Modulus = rd.ReadBytes(Helpers.DecodeIntegerSize(rd));

                RSAParameterTraits traits = new RSAParameterTraits(rsAparams.Modulus.Length * 8);

                rsAparams.Modulus = Helpers.AlignBytes(rsAparams.Modulus, traits.size_Mod);
                rsAparams.Exponent = Helpers.AlignBytes(rd.ReadBytes(Helpers.DecodeIntegerSize(rd)), traits.size_Exp);

                rsa.ImportParameters(rsAparams);
                return rsa;
            }
            catch (Exception)
            {
                return null;
            }
            finally
            {
                rd.Close();
            }
        }
        #endregion

        #region PKCS8 PRIVATE KEY BLOB TO RSACRYPTOPROVIDER
        public static RSACryptoServiceProvider DecodePrivateKeyInfo(byte[] pkcs8)
        {
            byte[] SeqOID = { 0x30, 0x0D, 0x06, 0x09, 0x2A, 0x86, 0x48, 0x86, 0xF7, 0x0D, 0x01, 0x01, 0x01, 0x05, 0x00 };
            byte[] seq = new byte[15];
            MemoryStream mem = new MemoryStream(pkcs8);
            int lenstream = (int)mem.Length;
            BinaryReader binr = new BinaryReader(mem); byte bt = 0;
            ushort twobytes = 0;

            try
            {

                twobytes = binr.ReadUInt16();
                if (twobytes == 0x8130) binr.ReadByte();
                else if (twobytes == 0x8230)
                    binr.ReadInt16();
                else
                    return null;


                bt = binr.ReadByte();
                if (bt != 0x02)
                    return null;

                twobytes = binr.ReadUInt16();

                if (twobytes != 0x0001)
                    return null;

                seq = binr.ReadBytes(15); if (!CompareBytearrays(seq, SeqOID)) return null;

                bt = binr.ReadByte();
                if (bt != 0x04) return null;

                bt = binr.ReadByte(); if (bt == 0x81)
                    binr.ReadByte();
                else
                if (bt == 0x82)
                    binr.ReadUInt16();

                byte[] rsaprivkey = binr.ReadBytes((int)(lenstream - mem.Position));
                RSACryptoServiceProvider rsacsp = DecodeRSAPrivateKey(rsaprivkey);
                return rsacsp;
            }

            catch (Exception)
            {
                return null;
            }

            finally { binr.Close(); }

        }
        #endregion

        public static RSACryptoServiceProvider DecodeRSAPrivateKey(byte[] privkey)
        {
            try
            {
                RSACryptoServiceProvider RSA = new RSACryptoServiceProvider();
                RSAParameters RSAparams = DecodeRSAPrivateKeyToRSAParam(privkey);
                RSA.ImportParameters(RSAparams);
                return RSA;
            }
            catch (Exception)
            {
                return null;
            }
        }

        #region UTIL CLASSES
        private class Helpers
        {
            public static bool CompareBytearrays(byte[] a, byte[] b)
            {
                if (a.Length != b.Length)
                    return false;
                int i = 0;
                foreach (byte c in a)
                {
                    if (c != b[i])
                        return false;
                    i++;
                }
                return true;
            }
            public static byte[] AlignBytes(byte[] inputBytes, int alignSize)
            {
                int inputBytesSize = inputBytes.Length;

                if ((alignSize != -1) && (inputBytesSize < alignSize))
                {
                    byte[] buf = new byte[alignSize];
                    for (int i = 0; i < inputBytesSize; ++i)
                    {
                        buf[i + (alignSize - inputBytesSize)] = inputBytes[i];
                    }
                    return buf;
                }
                else
                {
                    return inputBytes;
                }
            }

            public static int DecodeIntegerSize(System.IO.BinaryReader rd)
            {
                byte byteValue;
                int count;

                byteValue = rd.ReadByte();
                if (byteValue != 0x02) return 0;

                byteValue = rd.ReadByte();
                if (byteValue == 0x81)
                {
                    count = rd.ReadByte();
                }
                else if (byteValue == 0x82)
                {
                    byte hi = rd.ReadByte(); byte lo = rd.ReadByte();
                    count = BitConverter.ToUInt16(new[] { lo, hi }, 0);
                }
                else
                {
                    count = byteValue;
                }

                while (rd.ReadByte() == 0x00)
                {
                    count -= 1;
                }
                rd.BaseStream.Seek(-1, System.IO.SeekOrigin.Current);

                return count;
            }
        }

        private class RSAParameterTraits
        {
            public RSAParameterTraits(int modulusLengthInBits)
            {
                int assumedLength = -1;
                double logbase = Math.Log(modulusLengthInBits, 2);
                if (logbase == (int)logbase) //-V3024
                {
                    assumedLength = modulusLengthInBits;
                }
                else
                {
                    assumedLength = (int)(logbase + 1.0);
                    assumedLength = (int)(Math.Pow(2, assumedLength));
                    System.Diagnostics.Debug.Assert(false);
                }

                switch (assumedLength)
                {
                    case 512:
                        this.size_Mod = 0x40;
                        this.size_Exp = -1;
                        this.size_D = 0x40;
                        this.size_P = 0x20;
                        this.size_Q = 0x20;
                        this.size_DP = 0x20;
                        this.size_DQ = 0x20;
                        this.size_InvQ = 0x20;
                        break;
                    case 1024:
                        this.size_Mod = 0x80;
                        this.size_Exp = -1;
                        this.size_D = 0x80;
                        this.size_P = 0x40;
                        this.size_Q = 0x40;
                        this.size_DP = 0x40;
                        this.size_DQ = 0x40;
                        this.size_InvQ = 0x40;
                        break;
                    case 2048:
                        this.size_Mod = 0x100;
                        this.size_Exp = -1;
                        this.size_D = 0x100;
                        this.size_P = 0x80;
                        this.size_Q = 0x80;
                        this.size_DP = 0x80;
                        this.size_DQ = 0x80;
                        this.size_InvQ = 0x80;
                        break;
                    case 4096:
                        this.size_Mod = 0x200;
                        this.size_Exp = -1;
                        this.size_D = 0x200;
                        this.size_P = 0x100;
                        this.size_Q = 0x100;
                        this.size_DP = 0x100;
                        this.size_DQ = 0x100;
                        this.size_InvQ = 0x100;
                        break;
                    default:
                        System.Diagnostics.Debug.Assert(false); break;
                }
            }

            public int size_Mod = -1;
            public int size_Exp = -1;
            public int size_D = -1;
            public int size_P = -1;
            public int size_Q = -1;
            public int size_DP = -1;
            public int size_DQ = -1;
            public int size_InvQ = -1;
        }


        private class AsnMessage
        {
            private byte[] m_octets;
            private String m_format;

            internal int Length
            {
                get
                {
                    if (null == m_octets) { return 0; }
                    return m_octets.Length;
                }
            }

            internal AsnMessage(byte[] octets, String format)
            {
                m_octets = octets;
                m_format = format;
            }

            internal byte[] GetBytes()
            {
                if (null == m_octets)
                { return new byte[] { }; }

                return m_octets;
            }
            internal String GetFormat()
            { return m_format; }
        }

        private class AsnType
        {

            public AsnType(byte tag, byte octet)
            {
                m_raw = false;
                m_tag = new byte[] { tag };
                m_octets = new byte[] { octet };
            }

            public AsnType(byte tag, byte[] octets)
            {
                m_raw = false;
                m_tag = new byte[] { tag };
                m_octets = octets;
            }

            public AsnType(byte tag, byte[] length, byte[] octets)
            {
                m_raw = true;
                m_tag = new byte[] { tag };
                m_length = length;
                m_octets = octets;
            }

            private bool m_raw;

            private bool Raw
            {
                get { return m_raw; }
                set { m_raw = value; }
            }

            private byte[] m_tag;
            public byte[] Tag
            {
                get
                {
                    if (null == m_tag)
                        return EMPTY;
                    return m_tag;
                }
            }

            private byte[] m_length;
            public byte[] Length
            {
                get
                {
                    if (null == m_length)
                        return EMPTY;
                    return m_length;
                }
            }

            private byte[] m_octets;
            public byte[] Octets
            {
                get
                {
                    if (null == m_octets)
                    { return EMPTY; }
                    return m_octets;
                }
                set
                { m_octets = value; }
            }

            internal byte[] GetBytes()
            {
                if (true == m_raw)
                {
                    return Concatenate(
                      new byte[][] { m_tag, m_length, m_octets }
                    );
                }

                SetLength();

                if (0x05 == m_tag[0])
                {
                    return Concatenate(
                      new byte[][] { m_tag, m_octets }
                    );
                }

                return Concatenate(
                  new byte[][] { m_tag, m_length, m_octets }
                );
            }

            private void SetLength()
            {
                if (null == m_octets)
                {
                    m_length = ZERO;
                    return;
                }

                if (0x05 == m_tag[0])
                {
                    m_length = EMPTY;
                    return;
                }

                byte[] length = null;

                if (m_octets.Length < 0x80)
                {
                    length = new byte[1];
                    length[0] = (byte)m_octets.Length;
                }
                else if (m_octets.Length <= 0xFF)
                {
                    length = new byte[2];
                    length[0] = 0x81;
                    length[1] = (byte)((m_octets.Length & 0xFF));
                }


                else if (m_octets.Length <= 0xFFFF)
                {
                    length = new byte[3];
                    length[0] = 0x82;
                    length[1] = (byte)((m_octets.Length & 0xFF00) >> 8);
                    length[2] = (byte)((m_octets.Length & 0xFF));
                }

                else if (m_octets.Length <= 0xFFFFFF)
                {
                    length = new byte[4];
                    length[0] = 0x83;
                    length[1] = (byte)((m_octets.Length & 0xFF0000) >> 16);
                    length[2] = (byte)((m_octets.Length & 0xFF00) >> 8);
                    length[3] = (byte)((m_octets.Length & 0xFF));
                }
                else
                {
                    length = new byte[5];
                    length[0] = 0x84;
                    length[1] = (byte)((m_octets.Length & 0xFF000000) >> 24);
                    length[2] = (byte)((m_octets.Length & 0xFF0000) >> 16);
                    length[3] = (byte)((m_octets.Length & 0xFF00) >> 8);
                    length[4] = (byte)((m_octets.Length & 0xFF));
                }

                m_length = length;
            }

            private byte[] Concatenate(byte[][] values)
            {
                if (IsEmpty(values))
                    return new byte[] { };

                int length = 0;
                foreach (byte[] b in values)
                {
                    if (null != b) length += b.Length;
                }

                byte[] cated = new byte[length];

                int current = 0;
                foreach (byte[] b in values)
                {
                    if (null != b)
                    {
                        Array.Copy(b, 0, cated, current, b.Length);
                        current += b.Length;
                    }
                }

                return cated;
            }
        };
        #endregion

        #region UTIL METHODS


        public static RSAParameters DecodeRSAPrivateKeyToRSAParam(byte[] privkey)
        {
            RSAParameters RSAparams = new RSAParameters();
            byte[] MODULUS, E, D, P, Q, DP, DQ, IQ;

            MemoryStream mem = new MemoryStream(privkey);
            BinaryReader binr = new BinaryReader(mem); byte bt = 0;
            ushort twobytes = 0;
            int elems = 0;
            try
            {
                twobytes = binr.ReadUInt16();
                if (twobytes == 0x8130) binr.ReadByte();
                else if (twobytes == 0x8230)
                    binr.ReadInt16();
                else
                    return RSAparams;

                twobytes = binr.ReadUInt16();
                if (twobytes != 0x0102)
                    return RSAparams;
                bt = binr.ReadByte();
                if (bt != 0x00)
                    return RSAparams;


                elems = GetIntegerSize(binr);
                MODULUS = binr.ReadBytes(elems);

                elems = GetIntegerSize(binr);
                E = binr.ReadBytes(elems);

                elems = GetIntegerSize(binr);
                D = binr.ReadBytes(elems);

                elems = GetIntegerSize(binr);
                P = binr.ReadBytes(elems);

                elems = GetIntegerSize(binr);
                Q = binr.ReadBytes(elems);

                elems = GetIntegerSize(binr);
                DP = binr.ReadBytes(elems);

                elems = GetIntegerSize(binr);
                DQ = binr.ReadBytes(elems);

                elems = GetIntegerSize(binr);
                IQ = binr.ReadBytes(elems);


                RSAparams.Modulus = MODULUS;
                RSAparams.Exponent = E;
                RSAparams.D = D;
                RSAparams.P = P;
                RSAparams.Q = Q;
                RSAparams.DP = DP;
                RSAparams.DQ = DQ;
                RSAparams.InverseQ = IQ;
                return RSAparams;
            }
            catch (Exception)
            {
                return RSAparams;
            }
            finally { binr.Close(); }
        }

        private static int GetIntegerSize(BinaryReader binr)
        {
            byte bt = 0;
            byte lowbyte = 0x00;
            byte highbyte = 0x00;
            int count = 0;
            bt = binr.ReadByte();
            if (bt != 0x02) return 0;
            bt = binr.ReadByte();

            if (bt == 0x81)
                count = binr.ReadByte();
            else
            if (bt == 0x82)
            {
                highbyte = binr.ReadByte(); lowbyte = binr.ReadByte();
                byte[] modint = { lowbyte, highbyte, 0x00, 0x00 };
                count = BitConverter.ToInt32(modint, 0);
            }
            else
            {
                count = bt;
            }



            while (binr.ReadByte() == 0x00)
            {
                count -= 1;
            }
            binr.BaseStream.Seek(-1, SeekOrigin.Current); return count;
        }


        private static bool CompareBytearrays(byte[] a, byte[] b)
        {
            if (a.Length != b.Length)
                return false;
            int i = 0;
            foreach (byte c in a)
            {
                if (c != b[i])
                    return false;
                i++;
            }
            return true;
        }

        private static AsnType CreateOctetString(byte[] value)
        {
            if (IsEmpty(value))
            {
                return new AsnType(0x04, EMPTY);
            }

            return new AsnType(0x04, value);
        }

        private static AsnType CreateOctetString(AsnType value)
        {
            if (IsEmpty(value))
            {
                return new AsnType(0x04, 0x00);
            }

            return new AsnType(0x04, value.GetBytes());
        }

        private static AsnType CreateOctetString(AsnType[] values)
        {
            if (IsEmpty(values))
            {
                return new AsnType(0x04, 0x00);
            }

            return new AsnType(0x04, Concatenate(values));
        }

        private static AsnType CreateOctetString(String value)
        {
            if (IsEmpty(value))
            { return CreateOctetString(EMPTY); }

            int len = (value.Length + 255) / 256;

            List<byte> octets = new List<byte>();
            for (int i = 0; i < len; i++)
            {
                String s = value.Substring(i * 2, 2);
                byte b = 0x00;

                try
                { b = Convert.ToByte(s, 16); }
                catch (FormatException /*e*/) { break; }
                catch (OverflowException /*e*/) { break; }

                octets.Add(b);
            }

            return CreateOctetString(octets.ToArray());
        }

        private static AsnType CreateBitString(byte[] octets)
        {
            return CreateBitString(octets, 0);
        }

        private static AsnType CreateBitString(byte[] octets, uint unusedBits)
        {
            if (IsEmpty(octets))
            {
                return new AsnType(0x03, EMPTY);
            }

            if (!(unusedBits < 8))
            { throw new ArgumentException("Unused bits must be less than 8."); }

            byte[] b = Concatenate(new byte[] { (byte)unusedBits }, octets);
            return new AsnType(0x03, b);
        }

        private static AsnType CreateBitString(AsnType value)
        {
            if (IsEmpty(value))
            { return new AsnType(0x03, EMPTY); }

            return CreateBitString(value.GetBytes(), 0x00);
        }

        private static AsnType CreateBitString(AsnType[] values)
        {
            if (IsEmpty(values))
            { return new AsnType(0x03, EMPTY); }

            return CreateBitString(Concatenate(values), 0x00);
        }

        private static AsnType CreateBitString(String value)
        {
            if (IsEmpty(value))
            { return CreateBitString(EMPTY); }

            int lstrlen = value.Length;
            int unusedBits = 8 - (lstrlen % 8);
            if (8 == unusedBits) { unusedBits = 0; }

            for (int i = 0; i < unusedBits; i++)
            { value += "0"; }

            int loctlen = (lstrlen + 7) / 8;

            List<byte> octets = new List<byte>();
            for (int i = 0; i < loctlen; i++)
            {
                String s = value.Substring(i * 8, 8);
                byte b = 0x00;

                try
                { b = Convert.ToByte(s, 2); }

                catch (FormatException /*e*/) { unusedBits = 0; break; }
                catch (OverflowException /*e*/) { unusedBits = 0; break; }

                octets.Add(b);
            }

            return CreateBitString(octets.ToArray(), (uint)unusedBits);
        }

        private static byte[] ZERO = new byte[] { 0 };
        private static byte[] EMPTY = new byte[] { };

        private static bool IsZero(byte[] octets)
        {
            if (IsEmpty(octets))
            { return false; }

            bool allZeros = true;
            for (int i = 0; i < octets.Length; i++)
            {
                if (0 != octets[i])
                { allZeros = false; break; }
            }
            return allZeros;
        }

        private static bool IsEmpty(byte[] octets)
        {
            if (null == octets || 0 == octets.Length)
            { return true; }

            return false;
        }

        private static bool IsEmpty(String s)
        {
            if (null == s || 0 == s.Length)
            { return true; }

            return false;
        }

        private static bool IsEmpty(String[] strings)
        {
            if (null == strings || 0 == strings.Length)
                return true;

            return false;
        }

        private static bool IsEmpty(AsnType value)
        {
            if (null == value)
            { return true; }

            return false;
        }

        private static bool IsEmpty(AsnType[] values)
        {
            if (null == values || 0 == values.Length)
                return true;

            return false;
        }

        private static bool IsEmpty(byte[][] arrays)
        {
            if (null == arrays || 0 == arrays.Length)
                return true;

            return false;
        }

        private static AsnType CreateInteger(byte[] value)
        {
            if (IsEmpty(value))
            { return CreateInteger(ZERO); }

            return new AsnType(0x02, value);
        }

        private static AsnType CreateNull()
        {
            return new AsnType(0x05, new byte[] { 0x00 });
        }

        private static byte[] Duplicate(byte[] b)
        {
            if (IsEmpty(b))
            { return EMPTY; }

            byte[] d = new byte[b.Length];
            Array.Copy(b, d, b.Length);

            return d;
        }

        private static AsnType CreateIntegerPos(byte[] value)
        {
            byte[] i = null, d = Duplicate(value);

            if (IsEmpty(d)) { d = ZERO; }

            if (d.Length > 0 && d[0] > 0x7F)
            {
                i = new byte[d.Length + 1];
                i[0] = 0x00;
                Array.Copy(d, 0, i, 1, value.Length);
            }
            else
            {
                i = d;
            }

            return CreateInteger(i);
        }

        private static byte[] Concatenate(AsnType[] values)
        {
            if (IsEmpty(values))
                return new byte[] { };

            int length = 0;
            foreach (AsnType t in values)
            {
                if (null != t)
                { length += t.GetBytes().Length; }
            }

            byte[] cated = new byte[length];

            int current = 0;
            foreach (AsnType t in values)
            {
                if (null != t)
                {
                    byte[] b = t.GetBytes();

                    Array.Copy(b, 0, cated, current, b.Length);
                    current += b.Length;
                }
            }

            return cated;
        }

        private static byte[] Concatenate(byte[] first, byte[] second)
        {
            return Concatenate(new byte[][] { first, second });
        }

        private static byte[] Concatenate(byte[][] values)
        {
            if (IsEmpty(values))
                return new byte[] { };

            int length = 0;
            foreach (byte[] b in values)
            {
                if (null != b)
                { length += b.Length; }
            }

            byte[] cated = new byte[length];

            int current = 0;
            foreach (byte[] b in values)
            {
                if (null != b)
                {
                    Array.Copy(b, 0, cated, current, b.Length);
                    current += b.Length;
                }
            }

            return cated;
        }

        private static AsnType CreateSequence(AsnType[] values)
        {

            if (IsEmpty(values))
            { throw new ArgumentException("A sequence requires at least one value."); }

            return new AsnType((0x10 | 0x20), Concatenate(values));
        }

        private static AsnType CreateOid(String value)
        {
            if (IsEmpty(value))
                return null;

            String[] tokens = value.Split(new Char[] { ' ', '.' });

            if (IsEmpty(tokens))
                return null;

            UInt64 a = 0;

            List<UInt64> arcs = new List<UInt64>();

            foreach (String t in tokens)
            {
                if (t.Length == 0) { break; }

                try { a = Convert.ToUInt64(t, CultureInfo.InvariantCulture); }
                catch (FormatException /*e*/) { break; }
                catch (OverflowException /*e*/) { break; }

                arcs.Add(a);
            }

            if (0 == arcs.Count)
                return null;

            List<byte> octets = new List<byte>();

            a = arcs[0] * 40;
            if (arcs.Count >= 2) { a += arcs[1]; }
            octets.Add((byte)(a));

            for (int i = 2; i < arcs.Count; i++)
            {
                List<byte> temp = new List<byte>();

                UInt64 arc = arcs[i];

                do
                {
                    temp.Add((byte)(0x80 | (arc & 0x7F)));
                    arc >>= 7;
                } while (0 != arc);

                byte[] t = temp.ToArray();

                t[0] = (byte)(0x7F & t[0]);

                Array.Reverse(t);

                foreach (byte b in t)
                { octets.Add(b); }
            }

            return CreateOid(octets.ToArray());
        }

        private static AsnType CreateOid(byte[] value)
        {
            if (IsEmpty(value))
            { return null; }

            return new AsnType(0x06, value);
        }

        private static byte[] Compliment1s(byte[] value)
        {
            if (IsEmpty(value))
            { return EMPTY; }

            byte[] c = Duplicate(value);

            for (int i = c.Length - 1; i >= 0; i--)
            {
                c[i] = (byte)~c[i];
            }

            return c;
        }
        #endregion
    }
}