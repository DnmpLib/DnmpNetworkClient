using System;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using Mono.Unix.Native;
using Mono.Unix;
using NLog;

namespace DnmpNetworkClient.OSDependent.Parts.Tap.Impl
{
    internal class UnixTapInterface : ITapInterface
    {
        [StructLayout(LayoutKind.Sequential, Size = 40)]
        private struct IfReq
        {
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 16)]
            public readonly string Name;

            public short Flags;
        }

        private const int tapIff = 0x0002;
        private const int noPiIff = 0x1000;

        [DllImport("libc", EntryPoint = "ioctl", SetLastError = true)]
        private static extern int IoCtl(int descriptor, uint request, ref IfReq ifreq);

        private IfReq currentInterfaceInfo;
        private Stream currentStream;
        private PhysicalAddress currentPhysicalAddress;

        private readonly Logger logger = LogManager.GetCurrentClassLogger();

        public PhysicalAddress GetPhysicalAddress()
        {
            return currentPhysicalAddress;
        }

        public Stream Open()
        {
            var descriptor = Syscall.open("/dev/net/tun", OpenFlags.O_RDWR);
            if (descriptor < 0)
            {
                logger.Error($"/dev/net/tun open error: ret: {descriptor}; LastError: {Marshal.GetLastWin32Error()}");
                return null;
            }
            currentInterfaceInfo = new IfReq
            {
                Flags = tapIff | noPiIff
            };
            var ioctlRetCode = IoCtl(descriptor, 0x400454CA, ref currentInterfaceInfo);
            if (ioctlRetCode < 0)
            {
                logger.Error($"ioctl error: ret: {ioctlRetCode}; LastError: {Marshal.GetLastWin32Error()}");
                return null;
            }
            currentPhysicalAddress = NetworkInterface.GetAllNetworkInterfaces().First(x => x.Description == currentInterfaceInfo.Name).GetPhysicalAddress();
            logger.Info($"Opened TAP device: FD #{descriptor}; Name '{currentInterfaceInfo.Name}'; HW address: {currentPhysicalAddress}");
            currentStream = new RawUnixStream(descriptor);
            
            return currentStream;
        }
        
        private sealed class RawUnixStream : Stream, IDisposable
        {
            private const int invalidFileDescriptor = -1;
            
            public RawUnixStream(int fileDescriptor)
            {
                if (invalidFileDescriptor == fileDescriptor)
                    throw new ArgumentException(@"Invalid file descriptor", nameof(fileDescriptor));

                this.fileDescriptor = fileDescriptor;
            }

            private void AssertNotDisposed()
            {
                if (fileDescriptor == invalidFileDescriptor)
                    throw new ObjectDisposedException("Invalid File Descriptor");
            }

            public override bool CanRead => true;

            public override bool CanSeek => false;

            public override bool CanWrite => true;

            public override long Length => throw new NotSupportedException();

            public override long Position
            {
                get => throw new NotSupportedException();
                set => throw new NotSupportedException();
            }

            public override int Read(byte[] buffer, int offset, int count)
            {
                AssertNotDisposed();
                AssertValidBuffer(buffer, offset, count);

                if (buffer.Length == 0)
                    return 0;

                var pointer = Marshal.AllocHGlobal(count);
                var r = Syscall.read(fileDescriptor, pointer, (ulong)count);
                Marshal.Copy(pointer, buffer, offset, count);
                Marshal.FreeHGlobal(pointer);
                return (int)r;
            }

            private void AssertValidBuffer(byte[] buffer, int offset, int count)
            {
                if (buffer == null)
                    throw new ArgumentNullException(nameof(buffer));
                if (offset < 0)
                    throw new ArgumentOutOfRangeException(nameof(offset), @"< 0");
                if (count < 0)
                    throw new ArgumentOutOfRangeException(nameof(count), @"< 0");
                if (offset > buffer.Length)
                    throw new ArgumentException("destination offset is beyond array size");
                if (offset > buffer.Length - count)
                    throw new ArgumentException("would overrun buffer");
            }

            public override void Write(byte[] buffer, int offset, int count)
            {
                AssertNotDisposed();
                AssertValidBuffer(buffer, offset, count);
                if (buffer.Length == 0)
                    return;

                var pointer = Marshal.AllocHGlobal(count);
                Marshal.Copy(buffer, offset, pointer, count);
                Syscall.write(fileDescriptor, pointer, (ulong)count);
                Marshal.FreeHGlobal(pointer);
            }

            ~RawUnixStream()
            {
                Close();
            }

            public override void Close()
            {
                if (fileDescriptor == invalidFileDescriptor)
                    return;

                int r;
                do
                {
                    r = Syscall.close(fileDescriptor);
                } while (UnixMarshal.ShouldRetrySyscall(r));
                UnixMarshal.ThrowExceptionForLastErrorIf(r);
                fileDescriptor = invalidFileDescriptor;
                GC.SuppressFinalize(this);
            }

            void IDisposable.Dispose()
            {
                if (fileDescriptor != invalidFileDescriptor)
                    Close();
                GC.SuppressFinalize(this);
            }

            public override void Flush() {  }

            public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

            public override void SetLength(long value) => throw new NotSupportedException();

            private int fileDescriptor;
        }

        public void Close()
        {
            currentStream.Close();
            currentStream = null;
        }
    }
}
