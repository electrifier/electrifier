﻿using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Media;
using muxc = Microsoft.UI.Xaml.Controls;
using System.ComponentModel;
using System.Drawing;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System;
using Windows.Storage.FileProperties;
using Windows.Storage.Streams;
using Windows.Storage;

namespace electrifier.Services;

public class DosShellItem : INotifyPropertyChanged
{
    public IStorageItem StorageItem
    {
        get;
    }

    public string FileName => StorageItem.Name;

    public string FileType => StorageItem.Path;

    public bool IsFolder
    {
        get;
    }

    public bool IsFile => !IsFolder;

    public ImageIcon ShellIcon
    {
        get;        // TODO: this is not initialized yet
    }

    public DosShellItem(IStorageItem storageItem)
    {
        StorageItem = storageItem;
        IsFolder = storageItem.IsOfType(StorageItemTypes.Folder);

        //_ = LoadDefaultImages();


        /////////////////////////////////////////////////////////////
        ///

        /*
        var uri = new System.Uri("ms-appx:///images/logo.png");
        var file = Windows.Storage.StorageFile.GetFileFromApplicationUriAsync(uri);
         */


        //        new ResourceManager().MainResourceMap.GetValue("Files/foo.png").ValueAsString





        //////////////////////////////////////////////////
        ///
        //var bitmap = new BitmapImage();
        ////await bitmapSource.SetSourceAsync(bitmapStream);
        //ShellIcon = new ImageIcon
        //{
        //    Source = bitmap
        //};
        //ShellIcon.Source = "../Assets/Views/Workbench/Shell32 Default unknown File.ico";
        /*
         * 
         * ..\Assets\Views\Workbench\Shell32 Default Folder.ico
        var bitmapSource = new BitmapSource();
        await bitmapSource.SetSourceAsync(bitmapStream);
        var icon = new muxc.ImageIcon() { Source = bitmapSource };
        */
        //        ShellIcon.Source = new ImageSource()
        //                 ShellIcon = new ImageIcon();
        //                ShellIcon
        // "..\Assets\Views\Workbench\Shell32 Default unknown File.ico"

    }

    //protected async Task LoadDefaultImages()
    //{

    //    // using muxc = Microsoft.UI.Xaml.Controls;

    //    //var bitmapSource = new BitmapSource();
    //    //await bitmapSource.SetSourceAsync(bitmapStream);

    //    try
    //    {
    //                var uri = new System.Uri("ms-appx://../Assets/Views/Workbench/Shell32 Default unknown File.ico");
    //        //        var uri = new System.Uri("ms-appx://..\\Assets\\Views\\Workbench\\Shell32 Default Folder.ico");

    //        // ..\Assets\Views\Workbench\Shell32 Default Folder.ico
    //        //var storageFile = await Windows.Storage.StorageFile.GetFileFromApplicationUriAsync(uri);



    //        var bmpImage = new BitmapImage(uri);


    //        var icon = new muxc.ImageIcon() { Source = bmpImage };

    //        var icon2 = new muxc.ImageIcon();


    //        /*
    //                var uri = new System.Uri("ms-appx:///images/logo.png");
    //                var file = Windows.Storage.StorageFile.GetFileFromApplicationUriAsync(uri);

    //        */

    //    }
    //    catch(System.Exception ex)
    //    {
    //        var text = ex.Message;
    //    }
    //}



//    public async Task<BitmapImage> GetImageThumbnailAsync()
//    {
//        try
//        {
//            var bitmapImage = new BitmapImage();

//            // TODO: Load folder image from resource
//            if (IsFolder)
//            {

//                    // "..\Assets\Views\Workbench\Shell32 Default unknown File.ico"
////                bitmapImage.SetSourceAsync(bitmapImage)
//            }
//            else
//            {
//                //using (var thumbnail = await StorageFile.GetThumbnailAsync(ThumbnailMode.SingleItem))
//                //{
//                //    bitmapImage.SetSource(thumbnail);
//                //}
//            }

//            //using (var thumbnail = await StorageItem.GetThumbnailAsync(ThumbnailMode.SingleItem))
//            //{
//            //    // Create a bitmap to be the image source.
//            //    if (thumbnail != null)
//            //    {
//            //        bitmapImage.SetSource(thumbnail);
//            //        thumbnail.Dispose();
//            //    }
//            //}

//            return bitmapImage;
//        }
//        catch (Exception)
//        {
//            throw;
//        }
//    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}


/// <summary>
/// -> <seealso cref="SoftwareBitmapSource"/>
/// </summary>
//public ImageIcon ShellIcon
//{
//    get => _shellIcon;
//    private set
//    {
//    }
//}
//private readonly ImageIcon _shellIcon = new();




//ShellIcon = new ImageIcon { Source = "Assets/Views/Workbench/Shell32 Default unknown File.ico" };
// Source="ms-appx:///Assets/globe.png"
//ShellIcon = new ImageIcon {
//    Source = "ms-appx:///Assets /Views/Workbench/Shell32 Default unknown File.ico"
//};
