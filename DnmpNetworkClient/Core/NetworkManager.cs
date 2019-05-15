using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using DnmpLibrary.Interaction.Protocol;
using DnmpLibrary.Interaction.Protocol.EndPointFactoryImpl;
using DnmpLibrary.Interaction.Protocol.EndPointImpl;
using DnmpLibrary.Security.Cryptography.Asymmetric;
using DnmpLibrary.Security.Cryptography.Asymmetric.Impl;
using DnmpLibrary.Util;
using DnmpLibrary.Util.BigEndian;
using DnmpNetworkClient.Config;
using DnmpNetworkClient.Util;
using Newtonsoft.Json;
using NLog;

namespace DnmpNetworkClient.Core
{
    internal class NetworkManager
    {
        private static readonly Logger logger = LogManager.GetCurrentClassLogger();

        public class SavedNetwork
        {
            [JsonIgnore]
            public RSAParameters Key => RsaKeyUtil.DecodePrivateKeyInfo(KeyBytes).ExportParameters(true);

            [JsonIgnore]
            public Guid Id => new Guid(MD5.Create().ComputeHash(Key.Modulus.Concat(Key.Exponent).ToArray()));

            public byte[] KeyBytes;
            public string Name;
            public Dictionary<string, DateTime> SavedIpEndPoints = new Dictionary<string, DateTime>();

            public SavedNetwork(string name, byte[] keyBytes)
            {
                KeyBytes = keyBytes;
                Name = name;
                RsaKeyUtil.DecodeRsaPrivateKeyToRsaParam(KeyBytes);
            }

            public byte[] GenerateKeyData()
            {
                return KeyBytes;
            }

            public byte[] GenerateInviteData(int maxLength)
            {
                if (maxLength < 16)
                    throw new ArgumentException($@"{nameof(maxLength)} should be at least 18", nameof(maxLength));
                var memoryStream = new MemoryStream();
                var binaryWriter = new BigEndianBinaryWriter(memoryStream);
                var allEndPoints = SavedIpEndPoints.Select(x => x.Key).Select(Convert.FromBase64String).OrderBy(x => x.Length).ThenBy(x => Guid.NewGuid()).ToList();
                var needCount = 0;
                var totalLength = 16;
                while (totalLength < maxLength && needCount < allEndPoints.Count)
                {
                    totalLength += allEndPoints[needCount].Length + 2;
                    needCount++;
                }
                var inviteEndPoints = allEndPoints.Take(needCount).ToList();
                binaryWriter.Write(Id.ToByteArray());
                foreach (var endPoint in inviteEndPoints)
                {
                    binaryWriter.Write((ushort) endPoint.Length);
                    binaryWriter.Write(endPoint);
                }
                return memoryStream.ToArray();
            }

            public Tuple<IEnumerable<IEndPoint>, IAsymmetricKey> GetConnectionData()
            {
                return new Tuple<IEnumerable<IEndPoint>, IAsymmetricKey>(SavedIpEndPoints.Select(x => new RealIPEndPointFactory().DeserializeEndPoint(Convert.FromBase64String(x.Key))), new RsaAsymmetricKey
                {
                    KeyParameters = Key
                });
            }
        }

        private readonly Dictionary<Guid, SavedNetwork> savedNetworks;

        public Dictionary<Guid, SavedNetwork> SavedNetworks => new Dictionary<Guid, SavedNetwork>(savedNetworks);

        private readonly NetworksSaveConfig config;

        public NetworkManager(NetworksSaveConfig config)
        {
            this.config = config;
            savedNetworks = File.Exists(config.SaveFile) ? JsonConvert.DeserializeObject<Dictionary<Guid, SavedNetwork>>(File.ReadAllText(config.SaveFile)) : new Dictionary<Guid, SavedNetwork>();
            File.WriteAllText(config.SaveFile, JsonConvert.SerializeObject(savedNetworks));
            CleanUpVoid(null);
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
            logger.Debug($"Added {endPoint} to {networkId}");
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
            var binaryReader = new BigEndianBinaryReader(new MemoryStream(inviteCode));
            var networkId = new Guid(binaryReader.ReadBytes(16));
            var endPoints = new List<byte[]>();
            while (binaryReader.BaseStream.Length != binaryReader.BaseStream.Position)
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

        public void SaveNetworks()
        {
            SaveNetworks(config.SaveFile);
        }

        public void SaveNetworks(string file)
        {
            lock (networksSaveLock)
                File.WriteAllText(file, JsonConvert.SerializeObject(savedNetworks));
        }

        private void CleanUpVoid(object _)
        {
            CleanUpOldEndPoints(TimeSpan.FromMilliseconds(config.SavedEndPointTtl));
            EventQueue.AddEvent(CleanUpVoid, null, DateTime.Now + TimeSpan.FromMilliseconds(config.SavedEndPointsCleanUpInterval));
        }
    }
}
