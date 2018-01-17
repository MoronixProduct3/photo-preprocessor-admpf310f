using System;
using System.Threading.Tasks;
using Dropbox.Api;
using System.Configuration;
using System.IO;
using System.IO.Compression;
using System.Drawing;
using System.Linq;
using System.Diagnostics;

class Program
{
    static DropboxClient dbx;
    static DropboxSettings settings;

    static string zipPath = "./.dump.zip";
    static string tempRawPath = "./.temp";
    static string outPath = tempRawPath+"/out/";
    const int orientationId = 0x0112;

    static void Main(string[] args)
    {
        CleanFolder();

        settings = new DropboxSettings();
        dbx = new DropboxClient(settings.token);

        Task task = Task.Run(DownloadFolder);
        task.Wait();

        task = Task.Run(Decompress);
        task.Wait();

        File.Delete(zipPath);
        Console.WriteLine("Suppression du dossier compressé");

        // Generating final JPEGS
        ConvertToFormat();

        // Show files to user for transfer
        Process.Start("explorer.exe", Path.GetFullPath(outPath));

        Console.WriteLine("Terminé - Appuyez sur une touche pour supprimer les fichiers");
        Console.ReadKey();

        CleanFolder();
    }

    static void  CleanFolder()
    {
        try
        {
            Directory.Delete(tempRawPath, recursive: true);
            Console.WriteLine("Nettoyage du dossier");
        }
        catch (Exception)
        {
        }
    }

    static async Task DownloadFolder()
    {
        Console.Write("Téléchargement ...");

        var down = await dbx.Files.DownloadZipAsync(settings.folderPath);

        using (var fileStream = File.Create(zipPath))
        {
            Stream data = await down.GetContentAsStreamAsync();
            data.CopyTo(fileStream);
        }

        Console.WriteLine(" Terminé");
    }

    static async Task Decompress()
    {
        Console.Write("Décompression ...");

        ZipFile.ExtractToDirectory(zipPath, tempRawPath);

        Console.WriteLine(" Terminé");
    }

    static async void ConvertToFormat()
    {
        string[] imagePaths = Directory.GetFiles(tempRawPath+settings.folderPath);

        Directory.CreateDirectory(outPath);

        Console.WriteLine("Conversion de {0} images:", imagePaths.Length);

        int i = 0;

        foreach (string imgPath in imagePaths)
        {
            Console.Write("Image {0}: {1} ...", ++i, imgPath);

            try
            {
                Image img = Image.FromFile(imgPath);

                string path = Path.GetFullPath(outPath + i.ToString() + ".jpg");

                if (img.PropertyIdList.Contains(orientationId))
                {
                    var pItem = img.GetPropertyItem(orientationId);
                    RotateFlipType fType = GetRotateFlipTypeByExifOrientationData(pItem.Value[0]);

                    if (fType != RotateFlipType.RotateNoneFlipNone)
                        img.RotateFlip(fType);
                }

                img.Save(path, System.Drawing.Imaging.ImageFormat.Jpeg);

                Console.WriteLine(" > {0}.jpg", i);

                img.Dispose();
            }
            catch (Exception)
            {
                Console.WriteLine(" [Erreur!]");
            }

        }
    }

    static RotateFlipType GetRotateFlipTypeByExifOrientationData(int orientation)
    {
        switch (orientation)
        {
            case 1:
            default:
                return RotateFlipType.RotateNoneFlipNone;
            case 2:
                return RotateFlipType.RotateNoneFlipX;
            case 3:
                return RotateFlipType.Rotate180FlipNone;
            case 4:
                return RotateFlipType.Rotate180FlipX;
            case 5:
                return RotateFlipType.Rotate90FlipX;
            case 6:
                return RotateFlipType.Rotate90FlipNone;
            case 7:
                return RotateFlipType.Rotate270FlipX;
            case 8:
                return RotateFlipType.Rotate270FlipNone;
        }
    }
}

class DropboxSettings
{
    public string token, folderPath;

    public DropboxSettings()
    {
        try
        {
            token = ConfigurationManager.AppSettings.Get("dropboxToken");
            folderPath = ConfigurationManager.AppSettings.Get("dropboxFolder");
        }
        catch (Exception)
        {
            throw new Exception("Set the App.config file with the appropriate values");
        }

        if (token.Trim().Equals("") || folderPath.Trim().Equals(""))
            throw new Exception("Set the App.config file with the appropriate values");
    }
}
