using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using DNMPLibrary.Interaction.Protocol;
using DNMPLibrary.Interaction.Protocol.EndPointFactoryImpl;
using DNMPLibrary.Interaction.Protocol.EndPointImpl;
using DNMPLibrary.Security;
using DNMPLibrary.Security.Cryptography.Asymmetric;
using DNMPLibrary.Security.Cryptography.Asymmetric.Impl;
using DNMPLibrary.Util;
using Newtonsoft.Json;

namespace DNMPWindowsClient
{
    internal class NetworkManager
    {
        public class SavedNetwork
        {
            [JsonIgnore]
            public RSAParameters Key => RSAKeyUtils.DecodePrivateKeyInfo(KeyBytes).ExportParameters(true);//RSAKeyUtils.DecodeRSAPrivateKeyToRSAParam(KeyBytes);

            [JsonIgnore]
            public Guid Id => new Guid(MD5.Create().ComputeHash(Key.Modulus.Concat(Key.Exponent).ToArray()));

            public byte[] KeyBytes;
            public string Name;
            public Dictionary<string, DateTime> SavedIpEndPoints = new Dictionary<string, DateTime>();

            public SavedNetwork(string name, byte[] keyBytes)
            {
                KeyBytes = keyBytes;
                Name = name;
                RSAKeyUtils.DecodeRSAPrivateKeyToRSAParam(KeyBytes);
            }

            public byte[] GenerateKeyData()
            {
                return KeyBytes;
            }

            public byte[] GenerateInviteData(int maxLength)
            {
                if (maxLength < 18)
                    throw new ArgumentException(@"maxLength should be at least 18", nameof(maxLength));
                var memoryStream = new MemoryStream();
                var binaryWriter = new BinaryWriter(memoryStream);
                var allEndPoints = SavedIpEndPoints.Select(x => x.Key).Select(Convert.FromBase64String).OrderBy(x => x.Length).ThenBy(x => Guid.NewGuid()).ToList(); // magic random shuffle
                var needCount = 0;
                var totalLength = 18;
                while (totalLength < maxLength && needCount < allEndPoints.Count)
                {
                    totalLength += allEndPoints[needCount].Length + 2;
                    needCount++;
                }
                var inviteEndPoints = allEndPoints.Take(needCount).ToList();
                binaryWriter.Write(Id.ToByteArray());
                binaryWriter.Write(inviteEndPoints.Count);
                foreach (var endPoint in inviteEndPoints)
                {
                    binaryWriter.Write((ushort) endPoint.Length);
                    binaryWriter.Write(endPoint);
                }
                return memoryStream.ToArray();
            }

            public Tuple<IEnumerable<IEndPoint>, IAsymmetricKey> GetConnectionData()
            {
                return new Tuple<IEnumerable<IEndPoint>, IAsymmetricKey>(SavedIpEndPoints.Select(x => new RealIPEndPointFactory().DeserializeEndPoint(Convert.FromBase64String(x.Key))), new RSAAsymmetricKey
                {
                    KeyParameters = Key
                });
            }
        }

        private readonly Dictionary<Guid, SavedNetwork> savedNetworks;

        public Dictionary<Guid, SavedNetwork> SavedNetworks => new Dictionary<Guid, SavedNetwork>(savedNetworks);

        public NetworkManager(string file)
        {
            savedNetworks = File.Exists(file) ? JsonConvert.DeserializeObject<Dictionary<Guid, SavedNetwork>>(File.ReadAllText(file)) : new Dictionary<Guid, SavedNetwork>();
            File.WriteAllText(file, JsonConvert.SerializeObject(savedNetworks));
        }

        public void CleanUpOldEndPoints(TimeSpan endPointTtl)
        {
            foreach (var network in savedNetworks)
            {
                var toDelete = new List<string>();
                foreach (var endPointPair in network.Value.SavedIpEndPoints)
                    if (DateTime.Now - endPointPair.Value > endPointTtl)
                        toDelete.Add(endPointPair.Key);
                foreach (var endPoint in toDelete)
                    network.Value.SavedIpEndPoints.Remove(endPoint);
            }
        }

        public void AddEndPoint(Guid networkId, RealIPEndPoint endPoint)
        {
            savedNetworks[networkId].SavedIpEndPoints[Convert.ToBase64String(new RealIPEndPointFactory().SerializeEndPoint(endPoint))] = DateTime.Now;
        }

        public Guid AddNetwork(string name, byte[] keyBytes)
        {
            var network = new SavedNetwork(name, keyBytes);
            if (savedNetworks.ContainsKey(network.Id))
                return network.Id;
                savedNetworks.Add(network.Id, network);
            return Guid.Empty;
        }

        public void RemoveNetwork(Guid networkId)
        {
            savedNetworks.Remove(networkId);
        }

        private static Tuple<Guid, List<byte[]>> ParseInviteCode(byte[] inviteCode)
        {
            var binaryReader = new BinaryReader(new MemoryStream(inviteCode));
            var networkId = new Guid(binaryReader.ReadBytes(16));
            var endPointCount = binaryReader.ReadInt32();
            var endPoints = new List<byte[]>();
            for (var i = 0; i < endPointCount; i++)
                endPoints.Add(binaryReader.ReadBytes(binaryReader.ReadUInt16()));
            return new Tuple<Guid, List<byte[]>>(networkId, endPoints);
        }

        public Tuple<Guid, int> AcceptInviteCode(byte[] inviteCode)
        {
            var result = ParseInviteCode(inviteCode);
            if (!savedNetworks.ContainsKey(result.Item1))
                return new Tuple<Guid, int>(result.Item1, result.Item2.Count);
            foreach (var endPoint in result.Item2)
                savedNetworks[result.Item1].SavedIpEndPoints[Convert.ToBase64String(endPoint)] = DateTime.Now;
            return new Tuple<Guid, int>(result.Item1, result.Item2.Count);
        }

        private readonly object networksSaveLock = new object();

        public void SaveNetworks(string file)
        {
            lock (networksSaveLock)
                File.WriteAllText(file, JsonConvert.SerializeObject(savedNetworks));
        }
    }
}
