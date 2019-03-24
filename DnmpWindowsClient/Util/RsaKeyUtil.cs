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
using System.Linq;
using System.Security.Cryptography;

namespace DnmpWindowsClient.Util
{
    internal class RsaKeyUtil
    {

        #region PUBLIC KEY TO X509 BLOB
        internal static byte[] PublicKeyToX509(RSAParameters publicKey)
        {
            var oid = CreateOid("1.2.840.113549.1.1.1");
            var algorithmId =
              CreateSequence(new[] { oid, CreateNull() });

            var n = CreateIntegerPos(publicKey.Modulus);
            var e = CreateIntegerPos(publicKey.Exponent);
            var key = CreateBitString(
              CreateSequence(new[] { n, e })
            );

            var publicKeyInfo =
              CreateSequence(new[] { algorithmId, key });

            return new AsnMessage(publicKeyInfo.GetBytes()).GetBytes();
        }
        #endregion BLOB

        #region PRIVATE KEY TO PKCS8 BLOB
        internal static byte[] PrivateKeyToPKCS8(RSAParameters privateKey)
        {
            var n = CreateIntegerPos(privateKey.Modulus);
            var e = CreateIntegerPos(privateKey.Exponent);
            var d = CreateIntegerPos(privateKey.D);
            var p = CreateIntegerPos(privateKey.P);
            var q = CreateIntegerPos(privateKey.Q);
            var dp = CreateIntegerPos(privateKey.DP);
            var dq = CreateIntegerPos(privateKey.DQ);
            var iq = CreateIntegerPos(privateKey.InverseQ);
            var version = CreateInteger(new byte[] { 0 });
            var key = CreateOctetString(
              CreateSequence(new[] { version, n, e, d, p, q, dp, dq, iq })
            );

            var algorithmId = CreateSequence(new[] { CreateOid("1.2.840.113549.1.1.1"), CreateNull() });

            var privateKeyInfo = CreateSequence(new[] { version, algorithmId, key });
            return new AsnMessage(privateKeyInfo.GetBytes()).GetBytes();
        }

        internal static byte[] PrivateKeyToPKCS8(byte[] privkey)
        {
            var rsaParameters = DecodeRsaPrivateKeyToRsaParam(privkey);
            var n = CreateIntegerPos(rsaParameters.Modulus);
            var e = CreateIntegerPos(rsaParameters.Exponent);
            var d = CreateIntegerPos(rsaParameters.D);
            var p = CreateIntegerPos(rsaParameters.P);
            var q = CreateIntegerPos(rsaParameters.Q);
            var dp = CreateIntegerPos(rsaParameters.DP);
            var dq = CreateIntegerPos(rsaParameters.DQ);
            var iq = CreateIntegerPos(rsaParameters.InverseQ);
            var version = CreateInteger(new byte[] { 0 });
            var key = CreateOctetString(
              CreateSequence(new[] { version, n, e, d, p, q, dp, dq, iq })
            );

            var algorithmId = CreateSequence(new[] { CreateOid("1.2.840.113549.1.1.1"), CreateNull() });

            var privateKeyInfo = CreateSequence(new[] { version, algorithmId, key });
            return new AsnMessage(privateKeyInfo.GetBytes()).GetBytes();
        }
        #endregion

        #region X509 PUBLIC KEY BLOB TO RSACRYPTOPROVIDER
        internal static RSACryptoServiceProvider DecodePublicKey(byte[] publicKeyBytes)
        {
            var ms = new MemoryStream(publicKeyBytes);
            var rd = new BinaryReader(ms);
            byte[] seqOid = { 0x30, 0x0D, 0x06, 0x09, 0x2A, 0x86, 0x48, 0x86, 0xF7, 0x0D, 0x01, 0x01, 0x01, 0x05, 0x00 };

            try
            {
                var shortValue = rd.ReadUInt16();

                switch (shortValue)
                {
                    case 0x8130:
                        rd.ReadByte(); break;
                    case 0x8230:
                        rd.ReadInt16(); break;
                    default:
                        return null;
                }

                var sequence = rd.ReadBytes(15);

                if (!Helpers.CompareByteArrays(sequence, seqOid))
                    return null;

                shortValue = rd.ReadUInt16();
                if (shortValue == 0x8103) rd.ReadByte();
                else if (shortValue == 0x8203)
                    rd.ReadInt16();
                else
                    return null;

                var byteValue = rd.ReadByte();
                if (byteValue != 0x00)
                    return null;

                shortValue = rd.ReadUInt16();
                if (shortValue == 0x8130) rd.ReadByte();
                else if (shortValue == 0x8230)
                    rd.ReadInt16();
                else
                    return null;


                var parms = new CspParameters
                {
                    Flags = CspProviderFlags.NoFlags,
                    KeyContainerName = Guid.NewGuid().ToString().ToUpperInvariant(),
                    ProviderType = Environment.OSVersion.Version.Major > 5 || Environment.OSVersion.Version.Major == 5 && Environment.OSVersion.Version.Minor >= 1 ? 0x18 : 1
                };

                var rsa = new RSACryptoServiceProvider(parms);
                var rsAparams = new RSAParameters
                {
                    Modulus = rd.ReadBytes(Helpers.DecodeIntegerSize(rd))
                };

                var traits = new RsaParameterTraits(rsAparams.Modulus.Length * 8);

                rsAparams.Modulus = Helpers.AlignBytes(rsAparams.Modulus, traits.SizeModulus);
                rsAparams.Exponent = Helpers.AlignBytes(rd.ReadBytes(Helpers.DecodeIntegerSize(rd)), traits.SizeExponent);

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
            byte[] seqOid = { 0x30, 0x0D, 0x06, 0x09, 0x2A, 0x86, 0x48, 0x86, 0xF7, 0x0D, 0x01, 0x01, 0x01, 0x05, 0x00 };
            var mem = new MemoryStream(pkcs8);
            var lenstream = (int)mem.Length;
            var binr = new BinaryReader(mem);

            try
            {

                var twobytes = binr.ReadUInt16();
                if (twobytes == 0x8130) binr.ReadByte();
                else if (twobytes == 0x8230)
                    binr.ReadInt16();
                else
                    return null;


                var bt = binr.ReadByte();
                if (bt != 0x02)
                    return null;

                twobytes = binr.ReadUInt16();

                if (twobytes != 0x0001)
                    return null;

                var sequence = binr.ReadBytes(15); if (!CompareBytearrays(sequence, seqOid)) return null;

                bt = binr.ReadByte();
                if (bt != 0x04) return null;

                bt = binr.ReadByte(); if (bt == 0x81)
                    binr.ReadByte();
                else
                if (bt == 0x82)
                    binr.ReadUInt16();

                var rsaprivkey = binr.ReadBytes((int)(lenstream - mem.Position));
                var rsacsp = DecodeRsaPrivateKey(rsaprivkey);
                return rsacsp;
            }

            catch (Exception)
            {
                return null;
            }

            finally { binr.Close(); }

        }
        #endregion

        public static RSACryptoServiceProvider DecodeRsaPrivateKey(byte[] privkey)
        {
            try
            {
                var rsaCryptoServiceProvider = new RSACryptoServiceProvider();
                var rsaParameters = DecodeRsaPrivateKeyToRsaParam(privkey);
                rsaCryptoServiceProvider.ImportParameters(rsaParameters);
                return rsaCryptoServiceProvider;
            }
            catch (Exception)
            {
                return null;
            }
        }

        #region UTIL CLASSES
        private static class Helpers
        {
            public static bool CompareByteArrays(byte[] a, byte[] b)
            {
                if (a.Length != b.Length)
                    return false;
                var i = 0;
                foreach (var c in a)
                {
                    if (c != b[i])
                        return false;
                    i++;
                }
                return true;
            }

            public static byte[] AlignBytes(byte[] inputBytes, int alignSize)
            {
                var inputBytesSize = inputBytes.Length;

                if (alignSize == -1 || inputBytesSize >= alignSize)
                    return inputBytes;
                var buf = new byte[alignSize];
                for (var i = 0; i < inputBytesSize; ++i)
                {
                    buf[i + (alignSize - inputBytesSize)] = inputBytes[i];
                }
                return buf;

            }

            public static int DecodeIntegerSize(BinaryReader rd)
            {
                int count;

                var byteValue = rd.ReadByte();
                if (byteValue != 0x02) return 0;

                byteValue = rd.ReadByte();
                if (byteValue == 0x81)
                {
                    count = rd.ReadByte();
                }
                else if (byteValue == 0x82)
                {
                    var hi = rd.ReadByte(); var lo = rd.ReadByte();
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
                rd.BaseStream.Seek(-1, SeekOrigin.Current);

                return count;
            }
        }

        private class RsaParameterTraits
        {
            public RsaParameterTraits(int modulusLengthInBits)
            {
                int assumedLength;
                var logbase = Math.Log(modulusLengthInBits, 2);
                // ReSharper disable once CompareOfFloatsByEqualityOperator
                if (logbase == (int)logbase) //-V3024
                {
                    assumedLength = modulusLengthInBits;
                }
                else
                {
                    assumedLength = (int)(logbase + 1.0);
                    assumedLength = (int)Math.Pow(2, assumedLength);
                    System.Diagnostics.Debug.Assert(false);
                }

                switch (assumedLength)
                {
                    case 512:
                        SizeModulus = 0x40;
                        SizeExponent = -1;
                        break;
                    case 1024:
                        SizeModulus = 0x80;
                        SizeExponent = -1;
                        break;
                    case 2048:
                        SizeModulus = 0x100;
                        SizeExponent = -1;
                        break;
                    case 4096:
                        SizeModulus = 0x200;
                        SizeExponent = -1;
                        break;
                    default:
                        System.Diagnostics.Debug.Assert(false); break;
                }
            }

            public readonly int SizeModulus = -1;
            public readonly int SizeExponent = -1;
        }


        private class AsnMessage
        {
            private readonly byte[] octets;

            internal AsnMessage(byte[] octets)
            {
                this.octets = octets;
            }

            internal byte[] GetBytes()
            {
                return octets ?? new byte[] { };
            }
        }

        private class AsnType
        {

            public AsnType(byte tag, byte octet)
            {
                Raw = false;
                this.tag = new[] { tag };
                octets = new[] { octet };
            }

            public AsnType(byte tag, byte[] octets)
            {
                Raw = false;
                this.tag = new[] { tag };
                this.octets = octets;
            }

            private bool Raw { get; }

            private readonly byte[] tag;

            private byte[] length;

            private readonly byte[] octets;

            internal byte[] GetBytes()
            {
                if (Raw)
                {
                    return ConcatenateArrays(
                      new[] { tag, length, octets }
                    );
                }

                SetLength();

                return ConcatenateArrays(0x05 == tag[0] ? new[] { tag, octets } : new[] { tag, length, octets });
            }

            private void SetLength()
            {
                if (null == octets)
                {
                    length = zero;
                    return;
                }

                if (0x05 == tag[0])
                {
                    length = empty;
                    return;
                }

                byte[] newLength;

                if (octets.Length < 0x80)
                {
                    newLength = new byte[1];
                    newLength[0] = (byte)octets.Length;
                }
                else if (octets.Length <= 0xFF)
                {
                    newLength = new byte[2];
                    newLength[0] = 0x81;
                    newLength[1] = (byte)(octets.Length & 0xFF);
                }


                else if (octets.Length <= 0xFFFF)
                {
                    newLength = new byte[3];
                    newLength[0] = 0x82;
                    newLength[1] = (byte)((octets.Length & 0xFF00) >> 8);
                    newLength[2] = (byte)(octets.Length & 0xFF);
                }

                else if (octets.Length <= 0xFFFFFF)
                {
                    newLength = new byte[4];
                    newLength[0] = 0x83;
                    newLength[1] = (byte)((octets.Length & 0xFF0000) >> 16);
                    newLength[2] = (byte)((octets.Length & 0xFF00) >> 8);
                    newLength[3] = (byte)(octets.Length & 0xFF);
                }
                else
                {
                    newLength = new byte[5];
                    newLength[0] = 0x84;
                    newLength[1] = (byte)((octets.Length & 0xFF000000) >> 24);
                    newLength[2] = (byte)((octets.Length & 0xFF0000) >> 16);
                    newLength[3] = (byte)((octets.Length & 0xFF00) >> 8);
                    newLength[4] = (byte)(octets.Length & 0xFF);
                }

                length = newLength;
            }

            private static byte[] ConcatenateArrays(byte[][] values)
            {
                if (IsEmpty(values))
                    return new byte[] { };

                var length = values.Where(b => null != b).Sum(b => b.Length);

                var cated = new byte[length];

                var current = 0;
                foreach (var b in values)
                {
                    if (null == b)
                        continue;
                    Array.Copy(b, 0, cated, current, b.Length);
                    current += b.Length;
                }

                return cated;
            }
        };
        #endregion

        #region UTIL METHODS


        public static RSAParameters DecodeRsaPrivateKeyToRsaParam(byte[] privkey)
        {
            var rsaParameters = new RSAParameters();

            var mem = new MemoryStream(privkey);
            var binr = new BinaryReader(mem);
            try
            {
                var twobytes = binr.ReadUInt16();
                if (twobytes == 0x8130) binr.ReadByte();
                else if (twobytes == 0x8230)
                    binr.ReadInt16();
                else
                    return rsaParameters;

                twobytes = binr.ReadUInt16();
                if (twobytes != 0x0102)
                    return rsaParameters;
                var bt = binr.ReadByte();
                if (bt != 0x00)
                    return rsaParameters;

                
                rsaParameters.Modulus = binr.ReadBytes(GetIntegerSize(binr));
                rsaParameters.Exponent = binr.ReadBytes(GetIntegerSize(binr));
                rsaParameters.D = binr.ReadBytes(GetIntegerSize(binr));
                rsaParameters.P = binr.ReadBytes(GetIntegerSize(binr));
                rsaParameters.Q = binr.ReadBytes(GetIntegerSize(binr));
                rsaParameters.DP = binr.ReadBytes(GetIntegerSize(binr));
                rsaParameters.DQ = binr.ReadBytes(GetIntegerSize(binr));
                rsaParameters.InverseQ = binr.ReadBytes(GetIntegerSize(binr));

                return rsaParameters;
            }
            catch (Exception)
            {
                return rsaParameters;
            }
            finally { binr.Close(); }
        }

        private static int GetIntegerSize(BinaryReader binr)
        {
            int count;
            var bt = binr.ReadByte();
            if (bt != 0x02) return 0;
            bt = binr.ReadByte();

            if (bt == 0x81)
                count = binr.ReadByte();
            else
            if (bt == 0x82)
            {
                var highbyte = binr.ReadByte();
                var lowbyte = binr.ReadByte();
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
            var i = 0;
            foreach (var c in a)
            {
                if (c != b[i])
                    return false;
                i++;
            }
            return true;
        }

        private static AsnType CreateOctetString(AsnType value)
        {
            return IsEmpty(value) ? new AsnType(0x04, 0x00) : new AsnType(0x04, value.GetBytes());
        }

        private static AsnType CreateBitString(byte[] octets, uint unusedBits = 0)
        {
            if (IsEmpty(octets))
            {
                return new AsnType(0x03, empty);
            }

            if (!(unusedBits < 8))
            { throw new ArgumentException("Unused bits must be less than 8."); }

            var b = Concatenate(new[] { (byte)unusedBits }, octets);
            return new AsnType(0x03, b);
        }

        private static AsnType CreateBitString(AsnType value)
        {
            return IsEmpty(value) ? new AsnType(0x03, empty) : CreateBitString(value.GetBytes());
        }

        private static readonly byte[] zero = { 0 };
        private static readonly byte[] empty = { };

        private static bool IsEmpty(byte[] octets) => null == octets || 0 == octets.Length;

        private static bool IsEmpty(string s) => string.IsNullOrEmpty(s);

        private static bool IsEmpty(string[] strings) => null == strings || 0 == strings.Length;

        private static bool IsEmpty(AsnType value) => null == value;

        private static bool IsEmpty(AsnType[] values) => null == values || 0 == values.Length;

        private static bool IsEmpty(byte[][] arrays) => null == arrays || 0 == arrays.Length;

        private static AsnType CreateInteger(byte[] value) => new AsnType(0x02, value);

        private static AsnType CreateNull() => new AsnType(0x05, new byte[] { 0x00 });

        private static byte[] Duplicate(byte[] b)
        {
            if (IsEmpty(b))
                return empty;

            var d = new byte[b.Length];
            Array.Copy(b, d, b.Length);

            return d;
        }

        private static AsnType CreateIntegerPos(byte[] value)
        {
            byte[] i, d = Duplicate(value);

            if (IsEmpty(d)) { d = zero; }

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

            var length = values.Where(t => null != t).Sum(t => t.GetBytes().Length);

            var cated = new byte[length];

            var current = 0;
            foreach (var t in values)
            {
                if (null == t)
                    continue;
                var b = t.GetBytes();

                Array.Copy(b, 0, cated, current, b.Length);
                current += b.Length;
            }

            return cated;
        }

        private static byte[] Concatenate(byte[] first, byte[] second)
        {
            return Concatenate(new[] { first, second });
        }

        private static byte[] Concatenate(byte[][] values)
        {
            if (IsEmpty(values))
                return new byte[] { };

            var length = values.Where(b => null != b).Sum(b => b.Length);

            var cated = new byte[length];

            var current = 0;
            foreach (var b in values)
            {
                if (null == b)
                    continue;
                Array.Copy(b, 0, cated, current, b.Length);
                current += b.Length;
            }

            return cated;
        }

        private static AsnType CreateSequence(AsnType[] values)
        {
            if (IsEmpty(values))
                throw new ArgumentException("A sequence requires at least one value.");

            return new AsnType(0x10 | 0x20, Concatenate(values));
        }

        private static AsnType CreateOid(string value)
        {
            if (IsEmpty(value))
                return null;

            var tokens = value.Split(' ', '.');

            if (IsEmpty(tokens))
                return null;

            ulong a;

            var arcs = new List<ulong>();

            foreach (var t in tokens)
            {
                if (t.Length == 0) { break; }

                try { a = Convert.ToUInt64(t, CultureInfo.InvariantCulture); }
                catch (FormatException /*e*/) { break; }
                catch (OverflowException /*e*/) { break; }

                arcs.Add(a);
            }

            if (0 == arcs.Count)
                return null;

            var octets = new List<byte>();

            a = arcs[0] * 40;
            if (arcs.Count >= 2) { a += arcs[1]; }
            octets.Add((byte)a);

            for (var i = 2; i < arcs.Count; i++)
            {
                var temp = new List<byte>();

                var arc = arcs[i];

                do
                {
                    temp.Add((byte)(0x80 | (arc & 0x7F)));
                    arc >>= 7;
                } while (0 != arc);

                var t = temp.ToArray();

                t[0] = (byte)(0x7F & t[0]);

                Array.Reverse(t);

                octets.AddRange(t);
            }

            return CreateOid(octets.ToArray());
        }

        private static AsnType CreateOid(byte[] value)
        {
            return IsEmpty(value) ? null : new AsnType(0x06, value);
        }
        #endregion
    }
}