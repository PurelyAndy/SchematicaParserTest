using System.IO;
using System.IO.Compression;
using System.Numerics;
using System.Text;

namespace SchematicaParserTest;

public struct Metadata
{
    public readonly IntVec3 EnclosingSize;
    public readonly string Author;
    public readonly string Description;
    public readonly string Name;
    public readonly int RegionCount;
    public readonly long TimeCreated;
    public readonly long TimeModified;
    public readonly int TotalBlocks;
    public readonly int TotalVolume;

    public Metadata(IntVec3 enclosingSize, string author, string description, string name, int regionCount,
        long timeCreated, long timeModified, int totalBlocks, int totalVolume)
    {
        EnclosingSize = enclosingSize;
        Author = author;
        Description = description;
        Name = name;
        RegionCount = regionCount;
        TimeCreated = timeCreated;
        TimeModified = timeModified;
        TotalBlocks = totalBlocks;
        TotalVolume = totalVolume;
    }
}

public struct Region
{
    public readonly string Name;
    public readonly IntVec3 Position;
    public readonly IntVec3 Size;
    public readonly List<BlockState> BlockStatePalette;

    public Region(string name, IntVec3 position, IntVec3 size, List<BlockState> blockStatePalette)
    {
        Name = name;
        Position = position;
        Size = size;
        BlockStatePalette = blockStatePalette;
    }
}

public struct BlockState
{
    public readonly string Name;

    public BlockState(string name)
    {
        Name = name;
    }
}

public class Litematic
{
    public readonly Metadata Metadata;
    public readonly List<Region> Regions;
    public readonly int MinecraftDataVersion;
    public readonly int SubVersion;
    public readonly int Version;

    public Litematic(Metadata metadata, List<Region> regions)
    {
        Metadata = metadata;
        Regions = regions;
    }
}

public class LitematicaParser
{
    private string _filePath;

    public LitematicaParser(string filePath)
    {
        _filePath = filePath;
    }

    public Litematic GetLitematic()
    {
        using MemoryStream memoryStream = new(File.ReadAllBytes(_filePath));
        using GZipStream gzipStream = new(memoryStream, CompressionMode.Decompress);
        using MemoryStream decompressedStream = new();
        gzipStream.CopyTo(decompressedStream);
        byte[] file = decompressedStream.ToArray();
        Metadata metadata = GetMetadata(file);
        List<Region> regions = GetRegions(file);
        return new(metadata, regions);
    }

    private Metadata GetMetadata(byte[] file)
    {
        byte[] metadataBytes = { 0x0A, 0x00, 0x08, 0x4D, 0x65, 0x74, 0x61, 0x64, 0x61, 0x74, 0x61, };
        int pos = 0;
        for (int i = 0; i < file.Length; i++)
        {
            for (int j = 0; j < file.Length; j++)
            {
                if (file[i + j] != metadataBytes[j])
                    break;
                if (j != metadataBytes.Length - 1)
                    continue;

                pos = i;
                break;
            }

            if (pos != 0)
                break;
        }

        pos += metadataBytes.Length + 14;
        string description = "";
        //big endian ushort
        ushort descriptionLength = (ushort)(file[pos] << 8 | file[pos + 1]);
        pos += 2;
        for (int i = pos; i < pos + descriptionLength; i++)
            description += (char)file[i];
        pos += descriptionLength;
        pos += 15;
        long timeModified = (long)file[pos] << 56 | (long)file[pos + 1] << 48 | (long)file[pos + 2] << 40 |
                            (long)file[pos + 3] << 32 | (long)file[pos + 4] << 24 | (long)file[pos + 5] << 16 |
                            (long)file[pos + 6] << 8 | file[pos + 7];
        pos += 8;
        pos += 14;
        long timeCreated = (long)file[pos] << 56 | (long)file[pos + 1] << 48 | (long)file[pos + 2] << 40 |
                           (long)file[pos + 3] << 32 | (long)file[pos + 4] << 24 | (long)file[pos + 5] << 16 |
                           (long)file[pos + 6] << 8 | file[pos + 7];
        pos += 8;
        pos += 14;
        int totalVolume = file[pos] << 24 | file[pos + 1] << 16 | file[pos + 2] << 8 | file[pos + 3];
        pos += 4;
        pos += 7;
        string name = "";
        //big endian ushort
        ushort nameLength = (ushort)(file[pos] << 8 | file[pos + 1]);
        pos += 2;
        for (int i = pos; i < pos + nameLength; i++)
            name += (char)file[i];
        pos += nameLength;
        pos += 9;
        string author = "";
        //big endian ushort
        ushort authorLength = (ushort)(file[pos] << 8 | file[pos + 1]);
        pos += 2;
        for (int i = pos; i < pos + authorLength; i++)
            author += (char)file[i];
        pos += authorLength;
        pos += 14;
        int totalBlocks = file[pos] << 24 | file[pos + 1] << 16 | file[pos + 2] << 8 | file[pos + 3];
        pos += 4;
        pos += 20;
        int z = file[pos] << 24 | file[pos + 1] << 16 | file[pos + 2] << 8 | file[pos + 3];
        pos += 4;
        pos += 4;
        int x = file[pos] << 24 | file[pos + 1] << 16 | file[pos + 2] << 8 | file[pos + 3];
        pos += 4;
        pos += 4;
        int y = file[pos] << 24 | file[pos + 1] << 16 | file[pos + 2] << 8 | file[pos + 3];
        IntVec3 enclosingSize = new(x, y, z);
        pos += 4;
        pos += 15; //i expected it to be 14, but there's an extra 0 byte at the end of Y for some reason
        int regionCount = file[pos] << 24 | file[pos + 1] << 16 | file[pos + 2] << 8 | file[pos + 3];

        return new(enclosingSize, author, description, name, regionCount, timeCreated, timeModified, totalBlocks,
            totalVolume);
    }

    private List<Region> GetRegions(byte[] file)
    {
        ReadOnlySpan<byte> subVersionBytes = new(new byte[]
            { 0x03, 0x00, 0x0A, 0x53, 0x75, 0x62, 0x56, 0x65, 0x72, 0x73, 0x69, 0x6F, 0x6E });
        int subVersionPos = 0;
        for (int i = subVersionPos; i < file.Length; i++)
        {
            for (int j = 0; j < subVersionBytes.Length; j++)
            {
                if (file[i + j] != subVersionBytes[j])
                    break;
                if (j != subVersionBytes.Length - 1)
                    continue;

                subVersionPos = i;
                break;
            }

            if (subVersionPos != 0)
                break;
        }

        int pos = 0;
        pos += 14;
        List<Region> regions = new();
        while (true)
        {
            string name = "";
            //big endian ushort
            ushort nameLength = (ushort)(file[pos] << 8 | file[pos + 1]);
            pos += 2;
            for (int i = pos; i < pos + nameLength; i++)
                name += (char)file[i];

            pos += nameLength;
            pos += 11;
            int z = file[pos] << 24 | file[pos + 1] << 16 | file[pos + 2] << 8 | file[pos + 3];
            pos += 4;
            pos += 4;
            int x = file[pos] << 24 | file[pos + 1] << 16 | file[pos + 2] << 8 | file[pos + 3];
            pos += 4;
            pos += 4;
            int y = file[pos] << 24 | file[pos + 1] << 16 | file[pos + 2] << 8 | file[pos + 3];
            IntVec3 size = new(x, y, z);
            pos += 4;
            pos += 4;

            byte[] blockStatePaletteBytes =
            {
                0x09, 0x00, 0x11, 0x42, 0x6C, 0x6F, 0x63, 0x6B, 0x53, 0x74, 0x61, 0x74, 0x65, 0x50, 0x61, 0x6C, 0x65,
                0x74, 0x74, 0x65, 0x74, 0x74, 0x65
            };

            int last = pos;
            for (int i = pos; i < file.Length; i++)
            {
                for (int j = 0; j < file.Length; j++)
                {
                    if (file[i + j] != blockStatePaletteBytes[j])
                        break;
                    if (j != blockStatePaletteBytes.Length - 1)
                        continue;

                    pos = i;
                    break;
                }

                if (pos != last)
                    break;
            }

            pos += blockStatePaletteBytes.Length + 6;
            pos += 8;
            byte[] blockNameBytes = { 0x08, 0x00, 0x04, 0x4E, 0x61, 0x6D, 0x65 };
            byte[] blockStatesBytes =
                { 0x0C, 0x00, 0x0B, 0x42, 0x6C, 0x6F, 0x63, 0x6B, 0x53, 0x74, 0x61, 0x74, 0x65, 0x73 };

            List<BlockState> blockStates = new();

            int endPos = 0;
            for (int i = pos; i < file.Length; i++)
            {
                for (int j = 0; j < file.Length; j++)
                {
                    if (file[i + j] != blockStatesBytes[j])
                        break;
                    if (j != blockStatesBytes.Length - 1)
                        continue;

                    endPos = i;
                    break;
                }

                if (endPos != 0)
                    break;
            }

            while (true)
            {
                int current = pos;
                for (int i = current; i < endPos; i++)
                {
                    for (int j = 0; j < endPos; j++)
                    {
                        if (file[i + j] != blockNameBytes[j])
                            break;
                        if (j != blockNameBytes.Length - 1)
                            continue;

                        pos = i;
                        break;
                    }

                    if (pos != current)
                        break;
                }

                if (pos == current)
                    break;

                pos += blockNameBytes.Length;
                //big endian ushort
                ushort blockNameLength = (ushort)(file[pos] << 8 | file[pos + 1]);
                pos += 2;
                string blockName = "";
                for (int i = pos; i < pos + blockNameLength; i++)
                    blockName += (char)file[i];

                pos += blockNameLength;

                blockStates.Add(new(blockName));
            }

            pos = endPos;

            byte[] positionBytes = { 0x0A, 0x00, 0x08, 0x50, 0x6F, 0x73, 0x69, 0x74, 0x69, 0x6F, 0x6E };

            for (int i = pos; i < file.Length; i++)
            {
                for (int j = 0; j < file.Length; j++)
                {
                    if (file[i + j] != positionBytes[j])
                        break;
                    if (j != positionBytes.Length - 1)
                        continue;

                    pos = i;
                    break;
                }

                if (pos != endPos)
                    break;
            }

            pos += positionBytes.Length;
            pos += 4;
            int regionZ = file[pos] << 24 | file[pos + 1] << 16 | file[pos + 2] << 8 | file[pos + 3];
            pos += 4;
            pos += 4;
            int regionX = file[pos] << 24 | file[pos + 1] << 16 | file[pos + 2] << 8 | file[pos + 3];
            pos += 4;
            pos += 4;
            int regionY = file[pos] << 24 | file[pos + 1] << 16 | file[pos + 2] << 8 | file[pos + 3];
            IntVec3 position = new(regionX, regionY, regionZ);
            pos += 4;
            pos += 4;

            byte[] pendingBlockTicksBytes =
            {
                0x09, 0x00, 0x11, 0x50, 0x65, 0x6E, 0x64, 0x69, 0x6E, 0x67, 0x42, 0x6C, 0x6F, 0x63, 0x6B, 0x54, 0x69,
                0x63, 0x6B, 0x73
            };
            //find pending block ticks, this signifies the end of this region
            int thePos = pos;
            for (int i = pos; i < file.Length; i++)
            {
                for (int j = 0; j < file.Length; j++)
                {
                    if (file[i + j] != pendingBlockTicksBytes[j])
                        break;
                    if (j != pendingBlockTicksBytes.Length - 1)
                        continue;

                    pos = i;
                    break;
                }

                if (pos != thePos)
                    break;
            }

            pos += pendingBlockTicksBytes.Length;
            byte[] OAOO = { 0x0A, 0x00 };

            thePos = pos;
            for (int i = pos; i < file.Length; i++)
            {
                for (int j = 0; j < file.Length; j++)
                {
                    if (file[i + j] != OAOO[j])
                        break;
                    if (j != OAOO.Length - 1)
                        continue;

                    pos = i;
                    break;
                }

                if (pos != thePos)
                    break;
            }



            regions.Add(new(name, position, size, blockStates));
            if (++pos > subVersionPos)
                break;
        }

        return regions;
    }
}

public struct IntVec3
{
    public int X;
    public int Y;
    public int Z;

    public IntVec3(int x, int y, int z)
    {
        X = x;
        Y = y;
        Z = z;
    }

    public override string ToString()
    {
        return $"X: {X}, Y: {Y}, Z: {Z}";
    }
}

internal class Program
{
    static void Main(string[] args)
    {
        //open the Unnamed.litematic file from %appdata%/.minecraft/schematics folder
        LitematicaParser litematicaParser =
            new(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                ".minecraft/schematics/MySchem.litematic"));
        Console.WriteLine("Hello, World!");
        Litematic litematic = litematicaParser.GetLitematic();

        /*Console.WriteLine("Author: " + litematic.Metadata.Author);
        Console.WriteLine("Name: " + litematic.Metadata.Name);
        Console.WriteLine("Description: " + litematic.Metadata.Description);
        Console.WriteLine("Region Count: " + litematic.Metadata.RegionCount);
        Console.WriteLine("Time Created: " + litematic.Metadata.TimeCreated);
        Console.WriteLine("Time Modified: " + litematic.Metadata.TimeModified);
        Console.WriteLine("Enclosing Size: " + litematic.Metadata.EnclosingSize);
        Console.WriteLine("Total Blocks: " + litematic.Metadata.TotalBlocks);
        Console.WriteLine("Total Volume: " + litematic.Metadata.TotalVolume);
        Console.WriteLine("Version: " + litematic.Version);
        Console.WriteLine("Sub Version: " + litematic.SubVersion);
        Console.WriteLine("Minecraft Data Version: " + litematic.MinecraftDataVersion);
        Console.WriteLine("Region Count: " + litematic.Regions.Count);
        Console.WriteLine("Regions:");
        foreach (Region region in litematic.Regions)
        {
            Console.WriteLine("\tName: " + region.Name);
            Console.WriteLine("\tPosition: " + region.Position);
            Console.WriteLine("\tSize: " + region.Size);
            Console.WriteLine("\tBlock State Palette Count: " + region.BlockStatePalette.Count);
            foreach (BlockState blockState in region.BlockStatePalette)
            {
                Console.WriteLine("\t\tName: " + blockState.Name);
            }
        }*/
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.Write("Author");
        Console.ForegroundColor = ConsoleColor.White;
        Console.Write(": ");
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine(litematic.Metadata.Author);
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.Write("Name");
        Console.ForegroundColor = ConsoleColor.White;
        Console.Write(": ");
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine(litematic.Metadata.Name);
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.Write("Description");
        Console.ForegroundColor = ConsoleColor.White;
        Console.Write(": ");
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine(litematic.Metadata.Description);
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.Write("Region Count");
        Console.ForegroundColor = ConsoleColor.White;
        Console.Write(": ");
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine(litematic.Metadata.RegionCount);
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.Write("Time Created");
        Console.ForegroundColor = ConsoleColor.White;
        Console.Write(": ");
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine(litematic.Metadata.TimeCreated);
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.Write("Time Modified");
        Console.ForegroundColor = ConsoleColor.White;
        Console.Write(": ");
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine(litematic.Metadata.TimeModified);
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.Write("Enclosing Size");
        Console.ForegroundColor = ConsoleColor.White;
        Console.Write(": ");
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine(litematic.Metadata.EnclosingSize);
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.Write("Total Blocks");
        Console.ForegroundColor = ConsoleColor.White;
        Console.Write(": ");
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine(litematic.Metadata.TotalBlocks);
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.Write("Total Volume");
        Console.ForegroundColor = ConsoleColor.White;
        Console.Write(": ");
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine(litematic.Metadata.TotalVolume);
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.Write("Version");
        Console.ForegroundColor = ConsoleColor.White;
        Console.Write(": ");
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine(litematic.Version);
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.Write("Sub Version");
        Console.ForegroundColor = ConsoleColor.White;
        Console.Write(": ");
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine(litematic.SubVersion);
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.Write("Minecraft Data Version");
        Console.ForegroundColor = ConsoleColor.White;
        Console.Write(": ");
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine(litematic.MinecraftDataVersion);
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.Write("Region Count");
        Console.ForegroundColor = ConsoleColor.White;
        Console.Write(": ");
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine(litematic.Regions.Count);
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.Write("Regions");
        Console.ForegroundColor = ConsoleColor.White;
        Console.WriteLine(": ");
        foreach (Region region in litematic.Regions)
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.Write("\tName");
            Console.ForegroundColor = ConsoleColor.White;
            Console.Write(": ");
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine(region.Name);
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.Write("\tPosition");
            Console.ForegroundColor = ConsoleColor.White;
            Console.Write(": ");
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine(region.Position);
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.Write("\tSize");
            Console.ForegroundColor = ConsoleColor.White;
            Console.Write(": ");
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine(region.Size);
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.Write("\tBlock State Palette Count");
            Console.ForegroundColor = ConsoleColor.White;
            Console.Write(": ");
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine(region.BlockStatePalette.Count);
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.Write("\tBlock State Palette");
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine(": ");
            foreach (BlockState blockState in region.BlockStatePalette)
            {
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.Write("\t\tName");
                Console.ForegroundColor = ConsoleColor.White;
                Console.Write(": ");
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine(blockState.Name);
            }
            Console.WriteLine();
        }
        Console.ForegroundColor = ConsoleColor.White;
        Console.WriteLine("Press any key to exit...");
        Console.ReadKey();
    }
}