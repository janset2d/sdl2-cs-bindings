using System.Text;
using Janset.SDL2.AbiTests.Infrastructure;
using Janset.SDL2.AbiTests.Infrastructure.Assets;
using Janset.SDL2.AbiTests.Infrastructure.Classification;
using Janset.SDL2.AbiTests.Infrastructure.Sdl;
using SDL2;
using static SDL2.SDLNative;

namespace Janset.SDL2.AbiTests.Upstream.Assets;

public sealed class RwopsAbiTests
{
    private const string HelloWorld = "Hello World!";
    private const string Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";

    [Test]
    [Category(AbiCategories.UpstreamPort)]
    [Category(AbiCategories.Mechanical)]
    [Category(AbiCategories.Assets)]
    [Category(AbiCategories.SdlRwops)]
    [UpstreamSdlTest("test/testautomation_rwops.c", "rwops_testParamNegative")]
    public async Task SDLRwFromInputs_Should_Return_Null_When_Parameters_Invalid()
    {
        using AbiTempDirectory temp = new();

        NegativeParameterResult result = TestNegativeParameters(temp.GetFilePath("something.bin"));

        await Assert.That(result.FileNullNull).IsTrue();
        await Assert.That(result.FileNullAppendMode).IsTrue();
        await Assert.That(result.FileNullInvalidMode).IsTrue();
        await Assert.That(result.FileEmptyMode).IsTrue();
        await Assert.That(result.FileNullMode).IsTrue();
        await Assert.That(result.MemoryNull).IsTrue();
        await Assert.That(result.MemoryZeroSize).IsTrue();
        await Assert.That(result.ConstMemoryZeroSize).IsTrue();
    }

    [Test]
    [Category(AbiCategories.Smoke)]
    [Category(AbiCategories.Mechanical)]
    [Category(AbiCategories.Assets)]
    [Category(AbiCategories.SdlRwops)]
    public async Task SDLRwFromMem_Should_Write_And_Read_Pinned_Memory()
    {
        RwopsResult result = WriteAndReadMemory();

        await Assert.That(result.Written).IsEqualTo(3UL);
        await Assert.That(result.SeekPosition).IsEqualTo(0L);
        await Assert.That(result.Read).IsEqualTo(3UL);
        await Assert.That(result.Text).IsEqualTo("SDL");
        await SdlAssert.Success(result.CloseResult, "SDL_RWclose");
    }

    [Test]
    [Category(AbiCategories.UpstreamPort)]
    [Category(AbiCategories.BehaviorCoverage)]
    [Category(AbiCategories.Assets)]
    [Category(AbiCategories.SdlRwops)]
    [UpstreamSdlTest("test/testautomation_rwops.c", "rwops_testMem")]
    public async Task SDLRwFromMem_Should_Match_Upstream_Generic_Read_Write_And_Seek_Semantics()
    {
        GenericRwopsResult result = TestMemoryGenericSemantics();

        await Assert.That(result.WrittenObjects).IsEqualTo(1UL);
        await Assert.That(result.SeekSetPosition).IsEqualTo(6L);
        await Assert.That(result.SeekResetPosition).IsEqualTo(0L);
        await Assert.That(result.ReadObjects).IsEqualTo((ulong)HelloWorld.Length);
        await Assert.That(result.Text).IsEqualTo(HelloWorld);
        await Assert.That(result.SeekCurrentPosition).IsEqualTo(HelloWorld.Length - 4L);
        await Assert.That(result.SeekEndPosition).IsEqualTo(HelloWorld.Length - 1L);
        await Assert.That(result.InvalidWhencePosition).IsEqualTo(-1L);
        await SdlAssert.Success(result.CloseResult, "SDL_RWclose");
    }

    [Test]
    [Category(AbiCategories.Smoke)]
    [Category(AbiCategories.Mechanical)]
    [Category(AbiCategories.Assets)]
    [Category(AbiCategories.SdlRwops)]
    public async Task SDLRwFromConstMem_Should_Read_Pinned_Memory()
    {
        RwopsResult result = ReadConstMemory();

        await Assert.That(result.SeekPosition).IsEqualTo(0L);
        await Assert.That(result.Read).IsEqualTo(11UL);
        await Assert.That(result.Text).IsEqualTo("hello world");
        await SdlAssert.Success(result.CloseResult, "SDL_RWclose");
    }

    [Test]
    [Category(AbiCategories.UpstreamPort)]
    [Category(AbiCategories.BehaviorCoverage)]
    [Category(AbiCategories.Assets)]
    [Category(AbiCategories.SdlRwops)]
    [UpstreamSdlTest("test/testautomation_rwops.c", "rwops_testConstMem")]
    public async Task SDLRwFromConstMem_Should_Match_Upstream_Generic_Read_Only_And_Seek_Semantics()
    {
        GenericRwopsResult result = TestConstMemoryGenericSemantics();

        await Assert.That(result.WrittenObjects).IsEqualTo(0UL);
        await Assert.That(result.SeekSetPosition).IsEqualTo(6L);
        await Assert.That(result.SeekResetPosition).IsEqualTo(0L);
        await Assert.That(result.ReadObjects).IsEqualTo((ulong)HelloWorld.Length);
        await Assert.That(result.Text).IsEqualTo(HelloWorld);
        await Assert.That(result.SeekCurrentPosition).IsEqualTo(HelloWorld.Length - 4L);
        await Assert.That(result.SeekEndPosition).IsEqualTo(HelloWorld.Length - 1L);
        await Assert.That(result.InvalidWhencePosition).IsEqualTo(-1L);
        await SdlAssert.Success(result.CloseResult, "SDL_RWclose");
    }

    [Test]
    [Category(AbiCategories.UpstreamPort)]
    [Category(AbiCategories.BehaviorCoverage)]
    [Category(AbiCategories.Assets)]
    [Category(AbiCategories.SdlRwops)]
    [UpstreamSdlTest("test/testautomation_rwops.c", "rwops_testFileRead")]
    public async Task SDLRwFromFile_Should_Match_Upstream_Read_Mode_Read_Only_And_Seek_Semantics()
    {
        using AbiTempDirectory temp = new();
        string path = temp.GetFilePath("rwops-read.bin");
        File.WriteAllText(path, HelloWorld, Encoding.ASCII);

        GenericRwopsResult result = TestFileGenericSemantics(path, "rb");

        await Assert.That(result.WrittenObjects).IsEqualTo(0UL);
        await Assert.That(result.SeekSetPosition).IsEqualTo(6L);
        await Assert.That(result.SeekResetPosition).IsEqualTo(0L);
        await Assert.That(result.ReadObjects).IsEqualTo((ulong)HelloWorld.Length);
        await Assert.That(result.Text).IsEqualTo(HelloWorld);
        await Assert.That(result.SeekCurrentPosition).IsEqualTo(HelloWorld.Length - 4L);
        await Assert.That(result.SeekEndPosition).IsEqualTo(HelloWorld.Length - 1L);
        await Assert.That(result.InvalidWhencePosition).IsEqualTo(-1L);
        await SdlAssert.Success(result.CloseResult, "SDL_RWclose");
    }

    [Test]
    [Category(AbiCategories.Smoke)]
    [Category(AbiCategories.Mechanical)]
    [Category(AbiCategories.Assets)]
    [Category(AbiCategories.SdlRwops)]
    public async Task SDLRwFromFile_Should_Write_And_Read_Temp_File()
    {
        using AbiTempDirectory temp = new();

        string path = temp.GetFilePath("rwops.bin");
        RwopsResult result = WriteAndReadFile(path);

        await Assert.That(File.Exists(path)).IsTrue();
        await Assert.That(result.Written).IsEqualTo(10UL);
        await Assert.That(result.SeekPosition).IsEqualTo(0L);
        await Assert.That(result.Read).IsEqualTo(10UL);
        await Assert.That(result.Text).IsEqualTo("hello file");
        await SdlAssert.Success(result.CloseResult, "SDL_RWclose");
    }

    [Test]
    [Category(AbiCategories.UpstreamPort)]
    [Category(AbiCategories.BehaviorCoverage)]
    [Category(AbiCategories.Assets)]
    [Category(AbiCategories.SdlRwops)]
    [UpstreamSdlTest("test/testautomation_rwops.c", "rwops_testFileWrite")]
    public async Task SDLRwFromFile_Should_Match_Upstream_Write_Mode_Read_Write_And_Seek_Semantics()
    {
        using AbiTempDirectory temp = new();

        GenericRwopsResult result = TestFileGenericSemantics(temp.GetFilePath("rwops-write.bin"), "w+b");

        await Assert.That(result.WrittenObjects).IsEqualTo(1UL);
        await Assert.That(result.SeekSetPosition).IsEqualTo(6L);
        await Assert.That(result.SeekResetPosition).IsEqualTo(0L);
        await Assert.That(result.ReadObjects).IsEqualTo((ulong)HelloWorld.Length);
        await Assert.That(result.Text).IsEqualTo(HelloWorld);
        await Assert.That(result.SeekCurrentPosition).IsEqualTo(HelloWorld.Length - 4L);
        await Assert.That(result.SeekEndPosition).IsEqualTo(HelloWorld.Length - 1L);
        await Assert.That(result.InvalidWhencePosition).IsEqualTo(-1L);
        await SdlAssert.Success(result.CloseResult, "SDL_RWclose");
    }

    [Test]
    [Category(AbiCategories.HeaderCoverage)]
    [Category(AbiCategories.Mechanical)]
    [Category(AbiCategories.Assets)]
    [Category(AbiCategories.SdlRwops)]
    public async Task SDLRwSizeAndTell_Should_Report_Size_Position_And_Eof_For_Const_Memory()
    {
        SizeTellEofResult result = TestConstMemorySizeTellAndEof();

        await Assert.That(result.Size).IsEqualTo(HelloWorld.Length);
        await Assert.That(result.InitialTell).IsEqualTo(0L);
        await Assert.That(result.ReadObjects).IsEqualTo((ulong)HelloWorld.Length);
        await Assert.That(result.Text).IsEqualTo(HelloWorld);
        await Assert.That(result.EofReadObjects).IsEqualTo(0UL);
        await Assert.That(result.FinalTell).IsEqualTo(HelloWorld.Length);
        await SdlAssert.Success(result.CloseResult, "SDL_RWclose");
    }

    [Test]
    [Category(AbiCategories.Smoke)]
    [Category(AbiCategories.BehaviorCoverage)]
    [Category(AbiCategories.Assets)]
    [Category(AbiCategories.SdlRwops)]
    public async Task SDLRwFromFile_Should_Append_Writes_When_Mode_Is_Append_Update()
    {
        using AbiTempDirectory temp = new();
        string path = temp.GetFilePath("rwops-append.bin");
        File.WriteAllText(path, "start", Encoding.ASCII);

        AppendUpdateResult result = TestFileAppendUpdateMode(path);

        await Assert.That(result.WrittenObjects).IsEqualTo(3UL);
        await Assert.That(result.SizeAfterWrite).IsEqualTo(8L);
        await Assert.That(result.SeekPosition).IsEqualTo(0L);
        await Assert.That(result.ReadObjects).IsEqualTo(8UL);
        await Assert.That(result.Text).IsEqualTo("startend");
        await Assert.That(File.ReadAllText(path, Encoding.ASCII)).IsEqualTo("startend");
        await SdlAssert.Success(result.CloseResult, "SDL_RWclose");
    }

    [Test]
    [Category(AbiCategories.UpstreamPort)]
    [Category(AbiCategories.BehaviorCoverage)]
    [Category(AbiCategories.Assets)]
    [Category(AbiCategories.SdlRwops)]
    [UpstreamSdlTest("test/testautomation_rwops.c", "rwops_testCompareRWFromMemWithRWFromFile")]
    public async Task SDLRwFromMem_And_SDLRwFromFile_Should_Return_Same_Read_And_EndSeek_Results()
    {
        using AbiTempDirectory temp = new();

        string path = temp.GetFilePath("rwops-alphabet.bin");
        File.WriteAllText(path, Alphabet, Encoding.ASCII);

        CompareResult[] results = CompareMemoryAndFileReads(path);

        await Assert.That(results.Length).IsEqualTo(5);
        foreach (CompareResult result in results)
        {
            await Assert.That(result.MemoryReadObjects).IsEqualTo(result.FileReadObjects);
            await Assert.That(result.MemoryEndPosition).IsEqualTo(result.FileEndPosition);
            await Assert.That(result.MemoryText).IsEqualTo(Alphabet);
            await Assert.That(result.FileText).IsEqualTo(Alphabet);
            await SdlAssert.Success(result.MemoryCloseResult, "SDL_RWclose(mem)");
            await SdlAssert.Success(result.FileCloseResult, "SDL_RWclose(file)");
        }
    }

    [Test]
    [Category(AbiCategories.UpstreamPort)]
    [Category(AbiCategories.BehaviorCoverage)]
    [Category(AbiCategories.Assets)]
    [Category(AbiCategories.SdlRwops)]
    [UpstreamSdlTest("test/testautomation_rwops.c", "rwops_testFileWriteReadEndian")]
    public async Task SDLRwFromFile_Should_Write_And_Read_Endian_Aware_Values()
    {
        using AbiTempDirectory temp = new();

        EndianResult[] results = TestEndianReadWrite(temp.GetFilePath("rwops-endian.bin"));

        await Assert.That(results.Length).IsEqualTo(3);
        foreach (EndianResult result in results)
        {
            await Assert.That(result.WriteBe16).IsEqualTo(1UL);
            await Assert.That(result.WriteBe32).IsEqualTo(1UL);
            await Assert.That(result.WriteBe64).IsEqualTo(1UL);
            await Assert.That(result.WriteLe16).IsEqualTo(1UL);
            await Assert.That(result.WriteLe32).IsEqualTo(1UL);
            await Assert.That(result.WriteLe64).IsEqualTo(1UL);
            await Assert.That(result.SeekPosition).IsEqualTo(0L);
            await Assert.That(result.ReadBe16).IsEqualTo(result.ExpectedBe16);
            await Assert.That(result.ReadBe32).IsEqualTo(result.ExpectedBe32);
            await Assert.That(result.ReadBe64).IsEqualTo(result.ExpectedBe64);
            await Assert.That(result.ReadLe16).IsEqualTo(result.ExpectedLe16);
            await Assert.That(result.ReadLe32).IsEqualTo(result.ExpectedLe32);
            await Assert.That(result.ReadLe64).IsEqualTo(result.ExpectedLe64);
            await SdlAssert.Success(result.CloseResult, "SDL_RWclose");
        }
    }

    [Test]
    [Category(AbiCategories.UpstreamPort)]
    [Category(AbiCategories.Mechanical)]
    [Category(AbiCategories.Assets)]
    [Category(AbiCategories.SdlRwops)]
    [UpstreamSdlTest("test/testautomation_rwops.c", "rwops_testAllocFree")]
    public async Task SDLAllocRW_Should_Return_Non_Null_Context_That_Can_Be_Freed()
    {
        SDL_RWops rwops = SDL_AllocRW();
        try
        {
            await Assert.That(rwops.IsNotNull).IsTrue();
        }
        finally
        {
            if (rwops.IsNotNull)
            {
                SDL_FreeRW(rwops);
            }
        }
    }

    private static unsafe RwopsResult WriteAndReadMemory()
    {
        byte[] buffer = new byte[8];
        byte[] payload = Encoding.ASCII.GetBytes("SDL");
        byte[] readBuffer = new byte[payload.Length];

        fixed (byte* bufferPointer = buffer)
        fixed (byte* payloadPointer = payload)
        fixed (byte* readPointer = readBuffer)
        {
            SDL_RWops rwops = SDL_RWFromMem((nint)bufferPointer, buffer.Length);
            if (rwops.IsNull)
            {
                throw new InvalidOperationException($"SDL_RWFromMem failed: {SdlError.Current}");
            }

            ulong written = 0;
            long seekPosition = -1;
            ulong read = 0;
            int closeResult;

            try
            {
                written = ToUInt64(SDL_RWwrite(rwops, (nint)payloadPointer, NativeSize(1), NativeSize(payload.Length)));
                seekPosition = SDL_RWseek(rwops, 0, RW_SEEK_SET);
                read = ToUInt64(SDL_RWread(rwops, (nint)readPointer, NativeSize(1), NativeSize(readBuffer.Length)));
            }
            finally
            {
                closeResult = SDL_RWclose(rwops);
            }

            return new RwopsResult(written, seekPosition, read, Encoding.ASCII.GetString(readBuffer), closeResult);
        }
    }

    private static unsafe RwopsResult ReadConstMemory()
    {
        byte[] payload = Encoding.ASCII.GetBytes("hello world");
        byte[] readBuffer = new byte[payload.Length];

        fixed (byte* payloadPointer = payload)
        fixed (byte* readPointer = readBuffer)
        {
            SDL_RWops rwops = SDL_RWFromConstMem((nint)payloadPointer, payload.Length);
            if (rwops.IsNull)
            {
                throw new InvalidOperationException($"SDL_RWFromConstMem failed: {SdlError.Current}");
            }

            long seekPosition = -1;
            ulong read = 0;
            int closeResult;

            try
            {
                seekPosition = SDL_RWseek(rwops, 0, RW_SEEK_SET);
                read = ToUInt64(SDL_RWread(rwops, (nint)readPointer, NativeSize(1), NativeSize(readBuffer.Length)));
            }
            finally
            {
                closeResult = SDL_RWclose(rwops);
            }

            return new RwopsResult(0, seekPosition, read, Encoding.ASCII.GetString(readBuffer), closeResult);
        }
    }

    private static unsafe RwopsResult WriteAndReadFile(string path)
    {
        byte[] payload = Encoding.ASCII.GetBytes("hello file");
        byte[] readBuffer = new byte[payload.Length];

        using PinnedUtf8 pinnedPath = SdlUtf8.Pin(path);
        using PinnedUtf8 pinnedMode = SdlUtf8.Pin("w+b");

        fixed (byte* payloadPointer = payload)
        fixed (byte* readPointer = readBuffer)
        {
            SDL_RWops rwops = SDL_RWFromFile(pinnedPath.Pointer, pinnedMode.Pointer);
            if (rwops.IsNull)
            {
                throw new InvalidOperationException($"SDL_RWFromFile failed: {SdlError.Current}");
            }

            ulong written = 0;
            long seekPosition = -1;
            ulong read = 0;
            int closeResult;

            try
            {
                written = ToUInt64(SDL_RWwrite(rwops, (nint)payloadPointer, NativeSize(1), NativeSize(payload.Length)));
                seekPosition = SDL_RWseek(rwops, 0, RW_SEEK_SET);
                read = ToUInt64(SDL_RWread(rwops, (nint)readPointer, NativeSize(1), NativeSize(readBuffer.Length)));
            }
            finally
            {
                closeResult = SDL_RWclose(rwops);
            }

            return new RwopsResult(written, seekPosition, read, Encoding.ASCII.GetString(readBuffer), closeResult);
        }
    }

    private static unsafe NegativeParameterResult TestNegativeParameters(string path)
    {
        byte[] alphabet = Encoding.ASCII.GetBytes(Alphabet);

        using PinnedUtf8 pinnedPath = SdlUtf8.Pin(path);
        using PinnedUtf8 appendMode = SdlUtf8.Pin("ab+");
        using PinnedUtf8 invalidMode = SdlUtf8.Pin("sldfkjsldkfj");
        using PinnedUtf8 emptyMode = SdlUtf8.Pin(string.Empty);

        fixed (byte* alphabetPointer = alphabet)
        {
            return new NegativeParameterResult(
                IsNullAndCloseUnexpected(SDL_RWFromFile(null, null)),
                IsNullAndCloseUnexpected(SDL_RWFromFile(null, appendMode.Pointer)),
                IsNullAndCloseUnexpected(SDL_RWFromFile(null, invalidMode.Pointer)),
                IsNullAndCloseUnexpected(SDL_RWFromFile(pinnedPath.Pointer, emptyMode.Pointer)),
                IsNullAndCloseUnexpected(SDL_RWFromFile(pinnedPath.Pointer, null)),
                IsNullAndCloseUnexpected(SDL_RWFromMem(0, 10)),
                IsNullAndCloseUnexpected(SDL_RWFromMem((nint)alphabetPointer, 0)),
                IsNullAndCloseUnexpected(SDL_RWFromConstMem((nint)alphabetPointer, 0)));
        }
    }

    private static unsafe GenericRwopsResult TestMemoryGenericSemantics()
    {
        byte[] buffer = new byte[HelloWorld.Length];

        fixed (byte* bufferPointer = buffer)
        {
            SDL_RWops rwops = SDL_RWFromMem((nint)bufferPointer, buffer.Length);
            if (rwops.IsNull)
            {
                throw new InvalidOperationException($"SDL_RWFromMem failed: {SdlError.Current}");
            }

            return TestGenericRwopsSemantics(rwops, shouldWrite: true);
        }
    }

    private static unsafe GenericRwopsResult TestConstMemoryGenericSemantics()
    {
        byte[] payload = Encoding.ASCII.GetBytes(HelloWorld);

        fixed (byte* payloadPointer = payload)
        {
            SDL_RWops rwops = SDL_RWFromConstMem((nint)payloadPointer, payload.Length);
            if (rwops.IsNull)
            {
                throw new InvalidOperationException($"SDL_RWFromConstMem failed: {SdlError.Current}");
            }

            return TestGenericRwopsSemantics(rwops, shouldWrite: false);
        }
    }

    private static unsafe GenericRwopsResult TestFileGenericSemantics(string path, string mode)
    {
        using PinnedUtf8 pinnedPath = SdlUtf8.Pin(path);
        using PinnedUtf8 pinnedMode = SdlUtf8.Pin(mode);

        SDL_RWops rwops = SDL_RWFromFile(pinnedPath.Pointer, pinnedMode.Pointer);
        if (rwops.IsNull)
        {
            throw new InvalidOperationException($"SDL_RWFromFile failed: {SdlError.Current}");
        }

        return TestGenericRwopsSemantics(rwops, shouldWrite: mode.Length > 0 && mode[0] == 'w');
    }

    private static unsafe GenericRwopsResult TestGenericRwopsSemantics(SDL_RWops rwops, bool shouldWrite)
    {
        byte[] payload = Encoding.ASCII.GetBytes(HelloWorld);
        byte[] readBuffer = new byte[payload.Length];

        fixed (byte* payloadPointer = payload)
        fixed (byte* readPointer = readBuffer)
        {
            ulong written = 0;
            long seekSetPosition = -1;
            long seekResetPosition = -1;
            ulong read = 0;
            long seekCurrentPosition = -1;
            long seekEndPosition = -1;
            long invalidWhencePosition = 0;
            int closeResult;

            try
            {
                SDL_RWseek(rwops, 0, RW_SEEK_SET);
                written = ToUInt64(SDL_RWwrite(rwops, (nint)payloadPointer, NativeSize(payload.Length), NativeSize(1)));
                seekSetPosition = SDL_RWseek(rwops, 6, RW_SEEK_SET);
                seekResetPosition = SDL_RWseek(rwops, 0, RW_SEEK_SET);
                read = ToUInt64(SDL_RWread(rwops, (nint)readPointer, NativeSize(1), NativeSize(readBuffer.Length)));
                seekCurrentPosition = SDL_RWseek(rwops, -4, RW_SEEK_CUR);
                seekEndPosition = SDL_RWseek(rwops, -1, RW_SEEK_END);
                invalidWhencePosition = SDL_RWseek(rwops, 0, 999);
            }
            finally
            {
                closeResult = SDL_RWclose(rwops);
            }

            ulong expectedWrite = shouldWrite ? 1UL : 0UL;
            if (written != expectedWrite)
            {
                throw new InvalidOperationException($"SDL_RWwrite returned {written}; expected {expectedWrite}.");
            }

            return new GenericRwopsResult(
                written,
                seekSetPosition,
                seekResetPosition,
                read,
                Encoding.ASCII.GetString(readBuffer),
                seekCurrentPosition,
                seekEndPosition,
                invalidWhencePosition,
                closeResult);
        }
    }

    private static unsafe SizeTellEofResult TestConstMemorySizeTellAndEof()
    {
        byte[] payload = Encoding.ASCII.GetBytes(HelloWorld);
        byte[] readBuffer = new byte[payload.Length];
        byte eofByte = 0;

        fixed (byte* payloadPointer = payload)
        fixed (byte* readPointer = readBuffer)
        {
            SDL_RWops rwops = SDL_RWFromConstMem((nint)payloadPointer, payload.Length);
            if (rwops.IsNull)
            {
                throw new InvalidOperationException($"SDL_RWFromConstMem failed: {SdlError.Current}");
            }

            long size = -1;
            long initialTell = -1;
            ulong read = 0;
            ulong eofRead = 0;
            long finalTell = -1;
            int closeResult;

            try
            {
                size = SDL_RWsize(rwops);
                initialTell = SDL_RWtell(rwops);
                read = ToUInt64(SDL_RWread(rwops, (nint)readPointer, NativeSize(1), NativeSize(readBuffer.Length)));
                eofRead = ToUInt64(SDL_RWread(rwops, (nint)(&eofByte), NativeSize(1), NativeSize(1)));
                finalTell = SDL_RWtell(rwops);
            }
            finally
            {
                closeResult = SDL_RWclose(rwops);
            }

            return new SizeTellEofResult(
                size,
                initialTell,
                read,
                Encoding.ASCII.GetString(readBuffer),
                eofRead,
                finalTell,
                closeResult);
        }
    }

    private static unsafe AppendUpdateResult TestFileAppendUpdateMode(string path)
    {
        byte[] payload = Encoding.ASCII.GetBytes("end");
        byte[] readBuffer = new byte[8];

        using PinnedUtf8 pinnedPath = SdlUtf8.Pin(path);
        using PinnedUtf8 pinnedMode = SdlUtf8.Pin("ab+");

        fixed (byte* payloadPointer = payload)
        fixed (byte* readPointer = readBuffer)
        {
            SDL_RWops rwops = SDL_RWFromFile(pinnedPath.Pointer, pinnedMode.Pointer);
            if (rwops.IsNull)
            {
                throw new InvalidOperationException($"SDL_RWFromFile failed: {SdlError.Current}");
            }

            ulong written = 0;
            long sizeAfterWrite = -1;
            long seekPosition = -1;
            ulong read = 0;
            int closeResult;

            try
            {
                written = ToUInt64(SDL_RWwrite(rwops, (nint)payloadPointer, NativeSize(1), NativeSize(payload.Length)));
                sizeAfterWrite = SDL_RWsize(rwops);
                seekPosition = SDL_RWseek(rwops, 0, RW_SEEK_SET);
                read = ToUInt64(SDL_RWread(rwops, (nint)readPointer, NativeSize(1), NativeSize(readBuffer.Length)));
            }
            finally
            {
                closeResult = SDL_RWclose(rwops);
            }

            return new AppendUpdateResult(
                written,
                sizeAfterWrite,
                seekPosition,
                read,
                Encoding.ASCII.GetString(readBuffer),
                closeResult);
        }
    }

    private static unsafe CompareResult[] CompareMemoryAndFileReads(string path)
    {
        byte[] alphabet = Encoding.ASCII.GetBytes(Alphabet);
        CompareResult[] results = new CompareResult[5];

        fixed (byte* alphabetPointer = alphabet)
        {
            for (int size = 5; size < 10; size++)
            {
                results[size - 5] = CompareMemoryAndFileRead(path, (nint)alphabetPointer, alphabet.Length, size);
            }
        }

        return results;
    }

    private static unsafe CompareResult CompareMemoryAndFileRead(string path, nint alphabetPointer, int alphabetLength, int size)
    {
        byte[] memoryBuffer = new byte[alphabetLength];
        byte[] fileBuffer = new byte[alphabetLength];

        using PinnedUtf8 pinnedPath = SdlUtf8.Pin(path);
        using PinnedUtf8 readMode = SdlUtf8.Pin("rb");

        fixed (byte* memoryBufferPointer = memoryBuffer)
        fixed (byte* fileBufferPointer = fileBuffer)
        {
            SDL_RWops memoryRwops = SDL_RWFromMem(alphabetPointer, alphabetLength);
            if (memoryRwops.IsNull)
            {
                throw new InvalidOperationException($"SDL_RWFromMem failed: {SdlError.Current}");
            }

            SDL_RWops fileRwops = SDL_RWFromFile(pinnedPath.Pointer, readMode.Pointer);
            if (fileRwops.IsNull)
            {
                SDL_RWclose(memoryRwops);
                throw new InvalidOperationException($"SDL_RWFromFile failed: {SdlError.Current}");
            }

            ulong memoryRead = 0;
            long memoryEnd = -1;
            int memoryCloseResult = -1;
            ulong fileRead = 0;
            long fileEnd = -1;
            int fileCloseResult = -1;

            try
            {
                memoryRead = ToUInt64(SDL_RWread(memoryRwops, (nint)memoryBufferPointer, NativeSize(size), NativeSize(6)));
                memoryEnd = SDL_RWseek(memoryRwops, 0, RW_SEEK_END);
                fileRead = ToUInt64(SDL_RWread(fileRwops, (nint)fileBufferPointer, NativeSize(size), NativeSize(6)));
                fileEnd = SDL_RWseek(fileRwops, 0, RW_SEEK_END);
            }
            finally
            {
                memoryCloseResult = SDL_RWclose(memoryRwops);
                fileCloseResult = SDL_RWclose(fileRwops);
            }

            return new CompareResult(
                memoryRead,
                fileRead,
                memoryEnd,
                fileEnd,
                Encoding.ASCII.GetString(memoryBuffer),
                Encoding.ASCII.GetString(fileBuffer),
                memoryCloseResult,
                fileCloseResult);
        }
    }

    private static EndianResult[] TestEndianReadWrite(string path)
    {
        EndianValues[] values =
        [
            new(0, 0, 0, 0, 0, 0),
            new(1, 1, 1, 1, 1, 1),
            new(0x1234, 0x12345678, 0x123456789abcdef0, 0x4321, 0x87654321, 0xfedcba9876543210),
        ];

        EndianResult[] results = new EndianResult[values.Length];
        for (int i = 0; i < values.Length; i++)
        {
            results[i] = TestEndianReadWrite(path, values[i]);
        }

        return results;
    }

    private static unsafe EndianResult TestEndianReadWrite(string path, EndianValues values)
    {
        using PinnedUtf8 pinnedPath = SdlUtf8.Pin(path);
        using PinnedUtf8 pinnedMode = SdlUtf8.Pin("w+b");

        SDL_RWops rwops = SDL_RWFromFile(pinnedPath.Pointer, pinnedMode.Pointer);
        if (rwops.IsNull)
        {
            throw new InvalidOperationException($"SDL_RWFromFile failed: {SdlError.Current}");
        }

        ulong writeBe16 = 0;
        ulong writeBe32 = 0;
        ulong writeBe64 = 0;
        ulong writeLe16 = 0;
        ulong writeLe32 = 0;
        ulong writeLe64 = 0;
        long seekPosition = -1;
        ushort readBe16 = 0;
        uint readBe32 = 0;
        ulong readBe64 = 0;
        ushort readLe16 = 0;
        uint readLe32 = 0;
        ulong readLe64 = 0;
        int closeResult;

        try
        {
            writeBe16 = ToUInt64(SDL_WriteBE16(rwops, values.Be16));
            writeBe32 = ToUInt64(SDL_WriteBE32(rwops, values.Be32));
            writeBe64 = ToUInt64(SDL_WriteBE64(rwops, values.Be64));
            writeLe16 = ToUInt64(SDL_WriteLE16(rwops, values.Le16));
            writeLe32 = ToUInt64(SDL_WriteLE32(rwops, values.Le32));
            writeLe64 = ToUInt64(SDL_WriteLE64(rwops, values.Le64));
            seekPosition = SDL_RWseek(rwops, 0, RW_SEEK_SET);
            readBe16 = SDL_ReadBE16(rwops);
            readBe32 = SDL_ReadBE32(rwops);
            readBe64 = SDL_ReadBE64(rwops);
            readLe16 = SDL_ReadLE16(rwops);
            readLe32 = SDL_ReadLE32(rwops);
            readLe64 = SDL_ReadLE64(rwops);
        }
        finally
        {
            closeResult = SDL_RWclose(rwops);
        }

        return new EndianResult(
            values.Be16,
            values.Be32,
            values.Be64,
            values.Le16,
            values.Le32,
            values.Le64,
            writeBe16,
            writeBe32,
            writeBe64,
            writeLe16,
            writeLe32,
            writeLe64,
            seekPosition,
            readBe16,
            readBe32,
            readBe64,
            readLe16,
            readLe32,
            readLe64,
            closeResult);
    }

    private static bool IsNullAndCloseUnexpected(SDL_RWops rwops)
    {
        if (rwops.IsNull)
        {
            return true;
        }

        SDL_RWclose(rwops);
        return false;
    }

#if NET462
    private static UIntPtr NativeSize(int value) => new((uint)value);

    private static ulong ToUInt64(UIntPtr value) => value.ToUInt64();
#else
    private static nuint NativeSize(int value) => (nuint)value;

    private static ulong ToUInt64(nuint value) => checked((ulong)value);
#endif

    private sealed record RwopsResult(ulong Written, long SeekPosition, ulong Read, string Text, int CloseResult);

    private sealed record NegativeParameterResult(
        bool FileNullNull,
        bool FileNullAppendMode,
        bool FileNullInvalidMode,
        bool FileEmptyMode,
        bool FileNullMode,
        bool MemoryNull,
        bool MemoryZeroSize,
        bool ConstMemoryZeroSize);

    private sealed record GenericRwopsResult(
        ulong WrittenObjects,
        long SeekSetPosition,
        long SeekResetPosition,
        ulong ReadObjects,
        string Text,
        long SeekCurrentPosition,
        long SeekEndPosition,
        long InvalidWhencePosition,
        int CloseResult);

    private sealed record SizeTellEofResult(
        long Size,
        long InitialTell,
        ulong ReadObjects,
        string Text,
        ulong EofReadObjects,
        long FinalTell,
        int CloseResult);

    private sealed record AppendUpdateResult(
        ulong WrittenObjects,
        long SizeAfterWrite,
        long SeekPosition,
        ulong ReadObjects,
        string Text,
        int CloseResult);

    private sealed record CompareResult(
        ulong MemoryReadObjects,
        ulong FileReadObjects,
        long MemoryEndPosition,
        long FileEndPosition,
        string MemoryText,
        string FileText,
        int MemoryCloseResult,
        int FileCloseResult);

    private readonly record struct EndianValues(ushort Be16, uint Be32, ulong Be64, ushort Le16, uint Le32, ulong Le64);

    private sealed record EndianResult(
        ushort ExpectedBe16,
        uint ExpectedBe32,
        ulong ExpectedBe64,
        ushort ExpectedLe16,
        uint ExpectedLe32,
        ulong ExpectedLe64,
        ulong WriteBe16,
        ulong WriteBe32,
        ulong WriteBe64,
        ulong WriteLe16,
        ulong WriteLe32,
        ulong WriteLe64,
        long SeekPosition,
        ushort ReadBe16,
        uint ReadBe32,
        ulong ReadBe64,
        ushort ReadLe16,
        uint ReadLe32,
        ulong ReadLe64,
        int CloseResult);
}
