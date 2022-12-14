/*
** 
**  electrifier
** 
**  Copyright 2017-19 Thorsten Jung, www.electrifier.org
**  
**  Licensed under the Apache License, Version 2.0 (the "License");
**  you may not use this file except in compliance with the License.
**  You may obtain a copy of the License at
**  
**      http://www.apache.org/licenses/LICENSE-2.0
**  
**  Unless required by applicable law or agreed to in writing, software
**  distributed under the License is distributed on an "AS IS" BASIS,
**  WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
**  See the License for the specific language governing permissions and
**  limitations under the License.
**
*/

/*** ADDITIONAL LICENSE NOTES: *************************************************
**
** Major parts of this code were contributed by David Hall, the author of Vanara
**
** Visit https://github.com/dahall/Vanara for more information
**
** Thank you very much, David! ************************************************/

using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

using Vanara.Extensions;
using Vanara.PInvoke;
using Vanara.Windows.Shell;

namespace electrifier.Core.Components.Controls
{
    public partial class ExplorerBrowserControl
        : UserControl
        , Shell32.IServiceProvider
        , Shell32.IExplorerPaneVisibility
        , Shell32.IExplorerBrowserEvents
        , Shell32.ICommDlgBrowser3
        , IMessageFilter
    {
        #region Fields ========================================================================================================

        protected Shell32.IExplorerBrowser explorerBrowser = default;

        /// <summary>
        /// Used for this.explorerBrowser.Advise() and this.explorerBrowser.Unadvise() calls
        /// </summary>
        private uint adviseEventsCookie;

        private readonly string propertyBagName = "electrifier.ExplorerBrowserControl";

        private ShellItem initialNavigationTarget = default;

        private Shell32.FOLDERVIEWMODE viewMode;

        /// <summary>
        /// Nested class ViewEvents acts as the connection to IShellView of ExplorerBrowser
        /// </summary>
        protected ExplorerBrowserControl.ViewEvents ebViewEvents;

        private readonly HRESULT HRESULT_CANCELLED = unchecked((int)0x800704C7);
        private readonly HRESULT HRESULT_RESOURCE_IN_USE = unchecked((int)0x800700AA);

        /// <summary>The direction argument for Navigate</summary>
        internal enum NavigationLogDirection
        {
            /// <summary>Navigates forward through the history log</summary>
            Forward,

            /// <summary>Navigates backward through the history log</summary>
            Backward
        }

        #endregion ============================================================================================================

        #region Properties ====================================================================================================

        /// <summary>
        /// The set of ShellItems in the Explorer Browser.
        /// </summary>
        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public IReadOnlyList<ShellItem> Items { get; }

        /// <summary>
        /// The set of selected ShellItems in the Explorer Browser.
        /// </summary>
        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public IReadOnlyList<ShellItem> SelectedItems { get; }      // TODO: => Vanara.Collections.EventedList

        /// <summary>
        /// Contains the navigation history of the ExplorerBrowser.
        /// </summary>
        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public NavigationLog History { get; }

        // TODO: Add test for initialization, ante creation reaturn initialnavigationtarget?
        // TODO: Do we need this at all? See ExplorerBrowserControl_Navigated
        public string CurrentLocation => this.History.CurrentLocation?.GetDisplayName(ShellItemDisplayString.DesktopAbsoluteParsing);

        public Shell32.FOLDERVIEWMODE ViewMode { get => this.viewMode; set => this.SetViewMode(value); }

        #endregion ============================================================================================================

        #region Published Events ==============================================================================================

        /// <summary>
        /// Fires when the Items collection changes.
        /// </summary>
        [Category("Action"), Description("Items changed.")]
        public event EventHandler ItemsChanged;

        /// <summary>
        /// Fires when the ExplorerBrowser view has finished enumerating files.
        /// </summary>
        [Category("Behavior"), Description("View is done enumerating files.")]
        public event EventHandler ItemsEnumerated;

        /// <summary>
        /// Fires when a navigation has been initiated, but is not yet complete.
        /// </summary>
        [Category("Action"), Description("Navigation initiated, but not complete.")]
        public event EventHandler<NavigatingEventArgs> Navigating;

        /// <summary>
        /// Fires when a navigation has been 'completed': no Navigating listener has canceled, and the ExplorerBorwser has
        /// created a new view. The view will be populated with new items asynchronously, and ItemsChanged will be fired
        /// to reflect this some time later.
        /// </summary>
        [Category("Action"), Description("Navigation complete.")]
        public event EventHandler<NavigatedEventArgs> Navigated;

        /// <summary>
        /// Fires when either a Navigating listener cancels the navigation, or if the operating system determines that
        /// navigation is not possible.
        /// </summary>
        [Category("Action"), Description("Navigation failed.")]
        public event EventHandler<NavigationFailedEventArgs> NavigationFailed;

        /// <summary>
        /// Fires when the SelectedItems collection changes.
        /// </summary>
        [Category("Behavior"), Description("Selection changed.")]
        public event EventHandler SelectionChanged;

        /// <summary>
        /// Fires when the item selected in the view has changed (i.e., a rename ).
        /// This is not the same as SelectionChanged.
        /// </summary>
        [Category("Action"), Description("Selected item has changed.")]
        public event EventHandler SelectedItemModified;

        public event EventHandler<ShellFolderViewModeChangedEventArgs> ShellFolderViewModeChanged;

        #region EventArgs =====================================================================================================

        /// <summary>
        /// Event argument for the Navigated event
        /// </summary>
        public class NavigatedEventArgs : EventArgs
        {
            /// <summary>The new location of the explorer browser</summary>
            public ShellItem NewLocation { get; set; }
        }

        /// <summary>
        /// Event argument for the Navigating event
        /// </summary>
        public class NavigatingEventArgs : EventArgs
        {
            /// <summary>Set to 'True' to cancel the navigation.</summary>
            public bool Cancel { get; set; }

            /// <summary>The location being navigated to</summary>
            public ShellItem PendingLocation { get; set; }
        }

        /// <summary>
        /// Event argument for the NavigatinoFailed event
        /// </summary>
        public class NavigationFailedEventArgs : EventArgs
        {
            /// <summary>The location the browser would have navigated to.</summary>
            public ShellItem FailedLocation { get; set; }
        }

        /// <summary>
        /// Event argument for NavigationLogChangedEvent
        /// </summary>
        public class NavigationLogEventArgs : EventArgs
        {
            /// <summary>Indicates CanNavigateBackward has changed</summary>
            public bool CanNavigateBackwardChanged { get; set; }

            /// <summary>Indicates CanNavigateForward has changed</summary>
            public bool CanNavigateForwardChanged { get; set; }

            /// <summary>Indicates the Locations collection has changed</summary>
            public bool LocationsChanged { get; set; }
        }

        public class ShellFolderViewModeChangedEventArgs : EventArgs
        {
            internal protected ShellFolderViewModeChangedEventArgs(Shell32.FOLDERVIEWMODE oldFolderViewMode, Shell32.FOLDERVIEWMODE newFolderViewMode)
            {
                this.OldFolderViewMode = oldFolderViewMode;
                this.NewFolderViewMode = newFolderViewMode;
            }

            public Shell32.FOLDERVIEWMODE OldFolderViewMode { get; }
            public Shell32.FOLDERVIEWMODE NewFolderViewMode { get; }
        }

        #endregion ============================================================================================================

        #endregion ============================================================================================================

        public ExplorerBrowserControl(ShellItem initialNavigationTarget = default)
        {
            this.InitializeComponent();
            this.Items = new ShellItemCollection(this, Shell32.SVGIO.SVGIO_ALLVIEW);
            this.SelectedItems = new ShellItemCollection(this, Shell32.SVGIO.SVGIO_SELECTION);
            this.History = new NavigationLog(this);

            // TODO: On Windows 10, navigate to Quick Access by default...
            // <seealso href="https://www.tenforums.com/tutorials/3123-clsid-key-guid-shortcuts-list-windows-10-a.html">
            this.initialNavigationTarget = (default == initialNavigationTarget) ?
                new ShellFolder(Shell32.KNOWNFOLDERID.FOLDERID_UsersFiles) :        // TODO: On Windows 10, go to quick access; use virtual method for getting this for easy customization
                new ShellFolder(initialNavigationTarget);

            // TODO: Draw background painting on ListView?
            // TODO: IFolderView.SetText
        }

        /// <summary>
        /// Clears the Explorer Browser of existing content, fills it with content from the specified container,
        /// and adds a new point to the Travel Log.
        /// </summary>
        /// <param name="shellItem">The shell container to navigate to.</param>
        public void NavigateTo(ShellItem shellItem)
        {
            if (default == shellItem)
                throw new ArgumentNullException(nameof(shellItem));

            // In case the control has not been created yet, just overwrite the initial navigation target
            if (default == this.explorerBrowser)
            {
                this.initialNavigationTarget = shellItem;
            }
            else
            {
                try
                {
                    this.explorerBrowser.BrowseToObject(shellItem.IShellItem, Shell32.SBSP.SBSP_ABSOLUTE);
                }
                catch (COMException e)
                {
                    if (e.ErrorCode == this.HRESULT_RESOURCE_IN_USE || e.ErrorCode == this.HRESULT_CANCELLED)
                    {
                        this.OnNavigationFailed(new NavigationFailedEventArgs { FailedLocation = shellItem });
                    }
                    else
                    {
                        throw new ArgumentException("Unable to browse to this shell item.", nameof(shellItem), e);
                    }
                }
                catch (Exception e)
                {
                    throw new ArgumentException("Unable to browse to this shell item.", nameof(shellItem), e);
                }
            }
        }

        /// <summary>
        /// Navigate within the navigation log. This does not change the set of locations in the navigation log.
        /// </summary>
        /// <param name="historyIndex">An index into the navigation logs Locations collection.</param>
        /// <returns>True if the navigation succeeded, false if it failed for any reason.</returns>
        public bool NavigateToHistoryIndex(int historyIndex) => this.History.NavigateLog(historyIndex);

        protected void SetSite(Shell32.IServiceProvider sp) => (this.explorerBrowser as Shell32.IObjectWithSite)?.SetSite(sp);

        /// <summary>
        /// Gets the IFolderView2 interface from the explorer browser.
        /// </summary>
        /// <returns>An <see cref="Shell32.IFolderView2"/> instance.</returns>
        protected Shell32.IFolderView2 GetFolderView2() => this.explorerBrowser?.GetCurrentView<Shell32.IFolderView2>();

        /// <summary>
        /// Gets the IShellView interface from the explorer browser.
        /// </summary>
        /// <returns>An <see cref="Shell32.IShellView"/> instance.</returns>
        protected Shell32.IShellView GetShellView() => this.explorerBrowser?.GetCurrentView<Shell32.IShellView>();

        /// <summary>
        /// Determines if <see cref="ExplorerBrowserControl"/> is currently able to have a valid item collection.
        /// Doing these checks before requesting a collection prevents from internal exceptions that may be raised.
        /// 
        /// Currently the following tests are applied:
        ///   Check for full initialization of control
        /// </summary>
        /// <remarks>Note this does not guarantee that there is a valid collection currently, but the requisites are met.</remarks>
        /// <returns>true, if this instance might have a valid collection of items, false if not.</returns>
        protected bool CanHaveItemCollection()
        {
            // If initialNavigationTarget is set, we are still in creation of this control
            if (default != this.initialNavigationTarget)
                return false;

            return true;
        }

        public int GetItemCount(Shell32.SVGIO option)
        {
            if (!this.CanHaveItemCollection())
                return 0;

            Shell32.IFolderView2 folderView = null;
            try
            {
                folderView = this.GetFolderView2();

                if (folderView is null)
                    return 0;

                return folderView.ItemCount(option);
            }
            finally
            {
                if (folderView != null)
                    Marshal.ReleaseComObject(folderView);
            }
        }

        public void SelectAll()
        {
            if (!this.CanHaveItemCollection())
                return;

            Shell32.IFolderView2 folderView = null;

            try
            {
                folderView = this.GetFolderView2();

                if (folderView != null)
                {
                    int itemCount = folderView.ItemCount(Shell32.SVGIO.SVGIO_ALLVIEW);

                    for (int i = 0; i < itemCount; i++)
                    {
                        folderView.SelectItem(i, Shell32.SVSIF.SVSI_SELECT);
                    }
                }
            }
            finally
            {
                if (folderView != null)
                    Marshal.ReleaseComObject(folderView);
            }
        }

        public void SelectNone()
        {
            if (!this.CanHaveItemCollection())
                return;

            Shell32.IFolderView2 folderView = null;

            try
            {
                folderView = this.GetFolderView2();

                if (folderView != null)
                    folderView.SelectItem(-1, Shell32.SVSIF.SVSI_DESELECTOTHERS);
            }
            finally
            {
                if (folderView != null)
                    Marshal.ReleaseComObject(folderView);
            }
        }

        public void InvertSelection()
        {
            if (!this.CanHaveItemCollection())
                return;

            Shell32.IFolderView2 folderView = null;
            Shell32.IShellView shellView = null;

            try
            {
                folderView = this.GetFolderView2();
                shellView = this.GetShellView();

                if (folderView != null && shellView != null)
                {
                    int itemCount = folderView.ItemCount(Shell32.SVGIO.SVGIO_ALLVIEW);

                    for (int i = 0; i < itemCount; i++)
                    {
                        Shell32.PIDL pidl = folderView.Item(i);
                        Shell32.SVSIF state = folderView.GetSelectionState(pidl);

                        if (state == Shell32.SVSIF.SVSI_DESELECT ||
                            state == Shell32.SVSIF.SVSI_FOCUSED && state != Shell32.SVSIF.SVSI_SELECT)
                            shellView.SelectItem((IntPtr)pidl, Shell32.SVSIF.SVSI_SELECT);
                        else
                            shellView.SelectItem((IntPtr)pidl, Shell32.SVSIF.SVSI_DESELECT);
                    }
                }
            }
            finally
            {
                if (shellView != null)
                    Marshal.ReleaseComObject(shellView);
                if (folderView != null)
                    Marshal.ReleaseComObject(folderView);
            }
        }

        /// <summary>
        /// Gets the items in the ExplorerBrowser as an IShellItemArray.
        ///
        /// May return null if IFolderView2 isn't available or the requested ItemsArray is an empty set.
        /// </summary>
        /// <param name="option">A valid <see cref="Shell32.SVGIO"/> option to restrict the returned item collection.</param>
        /// <returns>An <see cref="Shell32.IShellItemArray"/> of the requested items in the folder view,
        /// or null if IFolderView2 isn't available or the requested ItemsArray is an empty set.</returns>
        protected Shell32.IShellItemArray GetItemsArray(Shell32.SVGIO option)
        {
            if (!this.CanHaveItemCollection())
                return null;

            Shell32.IFolderView2 folderView = null;

            try
            {
                folderView = this.GetFolderView2();

                if (folderView is null)
                    return null;

                return folderView.Items<Shell32.IShellItemArray>(option);
            }
            finally
            {
                if (null != folderView)
                    Marshal.ReleaseComObject(folderView);
            }
        }

        protected void ViewModeUpdatePending()
        {
            Shell32.IFolderView2 folderView = null;

            try
            {
                folderView = this.GetFolderView2();

                if (folderView is null)
                    throw new NullReferenceException(nameof(folderView));

                Shell32.FOLDERVIEWMODE oldViewMode = this.ViewMode;

                folderView.GetViewModeAndIconSize(out Shell32.FOLDERVIEWMODE newViewMode, out int newImageSize);
                //AppContext.TraceDebug($"ACTUAL ViewMode_Settings: {newViewMode}, Size: {newImageSize}");

                // tajbender 23/11/19:
                // TODO: We won't get noticed if only IconSize has changed, but ViewMode stays the same
                // (e.g. switiching from "Large Icons" to "Extra Large Icons" View).

                //if (newViewMode != oldViewMode)
                {
                    this.viewMode = newViewMode;

                    this.ShellFolderViewModeChanged?.Invoke(this, new ShellFolderViewModeChangedEventArgs(oldViewMode, newViewMode));
                }
            }
            finally
            {
                if (null != folderView)
                    Marshal.ReleaseComObject(folderView);
            }
        }

        protected virtual void SetViewMode(Shell32.FOLDERVIEWMODE folderViewMode)
        {
            Shell32.IFolderView2 folderView = null;
            Shell32.FOLDERVIEWMODE oldViewMode = this.ViewMode;

            if (oldViewMode == folderViewMode)
            {
                AppContext.TraceWarning($"SetViewMode: Old mode equals new mode");
                return;
            }

            try
            {
                int imageSize;

                switch(folderViewMode)
                {
                    case Shell32.FOLDERVIEWMODE.FVM_THUMBNAIL:
                        imageSize = 256;
                        break;
                    case Shell32.FOLDERVIEWMODE.FVM_ICON:
                        imageSize = 96;
                        break;
                    case Shell32.FOLDERVIEWMODE.FVM_THUMBSTRIP:
                        imageSize = 48;
                        break;
                    case Shell32.FOLDERVIEWMODE.FVM_SMALLICON:
                        imageSize = 16;
                        break;
                    default:
                        imageSize = -1;
                        break;

                }


                // See https://stackoverflow.com/questions/21328334/how-to-get-large-thumbnails-in-hosted-explorer-browser-control
                //folderView.GetViewModeAndIconSize(out Shell32.FOLDERVIEWMODE puViewMode, out int pImageSize);
                //AppContext.TraceDebug($"Current ViewMode: {puViewMode}, {pImageSize}");

                folderView = this.GetFolderView2();

                folderView.SetViewModeAndIconSize(folderViewMode, imageSize);

                this.viewMode = folderViewMode;

                this.ShellFolderViewModeChanged?.Invoke(this, new ShellFolderViewModeChangedEventArgs(oldViewMode, folderViewMode));
            }
            finally
            {
                if (null != folderView)
                    Marshal.ReleaseComObject(folderView);
            }
        }





        /// <summary>
        /// Find the native control handle, remove its border style, then ask for a redraw.
        /// </summary>
        /// <remarks>
        /// [Note by dahall] There is an option (EBO_NOBORDER) to avoid showing a border on the native ExplorerBrowser
        /// control so we wouldn't have to remove it afterwards, but:
        ///  1. It's not implemented by the Windows API Code Pack
        ///  2. The flag doesn't seem to work anyway (tested on 7 and 8.1)
        /// For reference: 
        ///  EXPLORER_BROWSER_OPTIONS
        ///  <seealso href="https://msdn.microsoft.com/en-us/library/windows/desktop/bb762501(v=vs.85).aspx"/>
        /// </remarks>
        protected void ExplorerBrowser_RemoveWindowBorder()
        {
            var hwnd = User32.FindWindowEx(this.Handle, default, "ExplorerBrowserControl", default);
            var wndStyle = (User32.WindowStyles)User32.GetWindowLongAuto(hwnd, User32.WindowLongFlags.GWL_STYLE).ToInt32();

            _ = User32.SetWindowLong(hwnd, User32.WindowLongFlags.GWL_STYLE,
                (int)wndStyle.ClearFlags(User32.WindowStyles.WS_CAPTION | User32.WindowStyles.WS_BORDER));
            _ = User32.SetWindowPos(hwnd, default, 0, 0, 0, 0,
                (User32.SetWindowPosFlags.SWP_FRAMECHANGED | User32.SetWindowPosFlags.SWP_NOMOVE | User32.SetWindowPosFlags.SWP_NOSIZE));
        }

        /// <summary>
        /// Raises the <see cref="ItemsChanged"/> event.
        /// </summary>
        protected internal virtual void OnItemsChanged()
        {
            this.ItemsChanged?.Invoke(this, EventArgs.Empty);
            // taj 17.11.19: When ViewMode has been changed by the user, the Shell signals this by using ItemChanged-event
            this.ViewModeUpdatePending();
//            this.ShellFolderViewModeChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Raises the <see cref="ItemsEnumerated"/> event.
        /// </summary>
        protected internal virtual void OnItemsEnumerated() => this.ItemsEnumerated?.Invoke(this, EventArgs.Empty);

        /// <summary>
        /// Raises the <see cref="Navigating"/> event.
        /// </summary>
        protected internal virtual void OnNavigating(NavigatingEventArgs npevent, out bool cancelled)
        {
            cancelled = false;

            if (this.Navigating == null || npevent?.PendingLocation == null)
                return;

            foreach (var del in this.Navigating.GetInvocationList())
            {
                del.DynamicInvoke(new object[] { this, npevent });

                if (npevent.Cancel)
                    cancelled = true;
            }
        }

        /// <summary>
        /// Raises the <see cref="Navigated"/> event.
        /// </summary>
        protected internal virtual void OnNavigated(NavigatedEventArgs ncevent)
        {
            if (ncevent?.NewLocation == null)
                return;

            this.Navigated?.Invoke(this, ncevent);
        }

        /// <summary>
        /// Raises the <see cref="NavigationFailed"/> event.
        /// </summary>
        protected internal virtual void OnNavigationFailed(NavigationFailedEventArgs nfevent)
        {
            if (nfevent?.FailedLocation == null)
                return;

            this.NavigationFailed?.Invoke(this, nfevent);
        }

        /// <summary>
        /// Raises the <see cref="SelectionChanged"/> event.
        /// </summary>
        protected internal virtual void OnSelectionChanged() => this.SelectionChanged?.Invoke(this, EventArgs.Empty);

        /// <summary>
        /// Raises the <see cref="SelectedItemModified"/> event.
        /// </summary>
        protected internal virtual void OnSelectedItemModified() => this.SelectedItemModified?.Invoke(this, EventArgs.Empty);

        protected override void OnCreateControl()
        {
            base.OnCreateControl();

            if (false == this.DesignMode)
            {
                this.explorerBrowser = new Vanara.PInvoke.Shell32.IExplorerBrowser();

                if (this.explorerBrowser is null)
                    throw new COMException("Could not instantiate ExplorerBrowser!");

                // Set site to get notified of IExplorerPaneVisibility and ICommDlgBrowser events
                this.SetSite(this);

                // Advise of IExplorerBrowserEvents
                this.explorerBrowser.Advise(this, out this.adviseEventsCookie);

                // Create connection point to ExplorerBrowser's ShellView events
                this.ebViewEvents = new ExplorerBrowserControl.ViewEvents(this);

                this.explorerBrowser.Initialize(this.Handle, this.ClientRectangle,
                    new Shell32.FOLDERSETTINGS(Shell32.FOLDERVIEWMODE.FVM_AUTO, Shell32.FOLDERFLAGS.FWF_NONE));

                // Force an initial show frames so that IExplorerPaneVisibility works the first time it is set.
                // This also enables the control panel to be browsed to. If it is not set, then navigating to 
                // the control panel succeeds, but no items are visible in the view.
                this.explorerBrowser.SetOptions(Shell32.EXPLORER_BROWSER_OPTIONS.EBO_SHOWFRAMES);

                this.explorerBrowser.SetPropertyBag(this.propertyBagName);

                this.ExplorerBrowser_RemoveWindowBorder();

                // Do initial navigation on background thread
                this.BeginInvoke(new MethodInvoker(
                    delegate
                    {
                        this.NavigateTo(this.initialNavigationTarget);
                        this.initialNavigationTarget = default;
                    }));
            }
        }

        protected override void OnHandleDestroyed(EventArgs e)
        {
            if (!(this.explorerBrowser is null))
            {
                this.ebViewEvents.DisconnectShellView();

                this.explorerBrowser.Unadvise(this.adviseEventsCookie);
                this.SetSite(null);

                this.explorerBrowser.Destroy();

                Marshal.ReleaseComObject(this.explorerBrowser);
                this.explorerBrowser = null;
            }

            base.OnHandleDestroyed(e);
        }

        protected override void OnSizeChanged(EventArgs e)
        {
            this.explorerBrowser?.SetRect(IntPtr.Zero, this.ClientRectangle);

            base.OnSizeChanged(e);
        }

        /// <summary>
        /// Ask ExplorerBrowser to translate Window-Messages for proper keyboard input handling
        /// </summary>
        /// <param name="msg">A reference to the Window Message</param>
        /// <returns>True if successful, otherwise false</returns>
        bool IMessageFilter.PreFilterMessage(ref Message msg)
        {
            // TODO: Is never called! => Notify?
            return ((this.explorerBrowser as Shell32.IInputObject)?.TranslateAcceleratorIO(
                new Vanara.PInvoke.MSG
                {
                    message = (uint)msg.Msg,
                    hwnd = msg.HWnd,
                    wParam = msg.WParam,
                    lParam = msg.LParam
                }).Succeeded ?? false);
        }

        #region Implemented Interface: IServiceProvider =======================================================================

        public HRESULT QueryService(in Guid guidService, in Guid riid, out IntPtr ppvObject)
        {
            ppvObject = default;

            if (guidService.Equals(typeof(Shell32.IExplorerPaneVisibility).GUID))
            {
                ppvObject = Marshal.GetComInterfaceForObject(this, typeof(Shell32.IExplorerPaneVisibility));

                return HRESULT.S_OK;
            }
            else if (guidService.Equals(typeof(Shell32.ICommDlgBrowser).GUID))
            {
                // ICommDlgBrowserX is exposed to host the Shell Browser and control its behaviour
                //
                // According comments in ExplorerBrowser.QueryService default implementation, marshalling to
                // ICommDlgBrowser2 fails and causes exceptions.
                //
                // <see href="https://github.com/aybe/Windows-API-Code-Pack-1.1/blob/master/source/WindowsAPICodePack/Shell/ExplorerBrowser/ExplorerBrowser.cs"/>
                if (riid.Equals(typeof(Shell32.ICommDlgBrowser).GUID) || riid.Equals(typeof(Shell32.ICommDlgBrowser3).GUID))
                {
                    ppvObject = Marshal.GetComInterfaceForObject(this, typeof(Shell32.ICommDlgBrowser3));

                    return HRESULT.S_OK;
                }
            }

            return HRESULT.E_NOINTERFACE;
        }

        #endregion ============================================================================================================

        #region Implemented Interface: IExplorerPaneVisibility ================================================================

        public HRESULT GetPaneState(in Guid ep, out Shell32.EXPLORERPANESTATE peps)
        {
            if (ep.Equals(Shell32.IExplorerPaneVisibilityConstants.EP_NavPane))
                peps = Shell32.EXPLORERPANESTATE.EPS_DEFAULT_ON;
            else
                peps = Shell32.EXPLORERPANESTATE.EPS_DEFAULT_OFF | Shell32.EXPLORERPANESTATE.EPS_FORCE;

            return HRESULT.S_OK;
        }

        #endregion ============================================================================================================

        #region Implemented Interface: IExplorerBrowserEvents =================================================================

        public HRESULT OnNavigationPending(IntPtr pidlFolder)
        {
            this.OnNavigating(new NavigatingEventArgs { PendingLocation = new ShellItem(pidlFolder) }, out var cancelled);

            return cancelled ? this.HRESULT_CANCELLED : HRESULT.S_OK;
        }

        public HRESULT OnViewCreated(Shell32.IShellView psv)
        {
            this.ebViewEvents.ConnectShellView(psv);

            return HRESULT.S_OK;
        }

        public HRESULT OnNavigationComplete(IntPtr pidlFolder)
        {
            this.ViewModeUpdatePending();
            this.OnNavigated(new NavigatedEventArgs { NewLocation = new ShellItem(pidlFolder) });

            return HRESULT.S_OK;
        }

        public HRESULT OnNavigationFailed(IntPtr pidlFolder)
        {
            this.OnNavigationFailed(new NavigationFailedEventArgs { FailedLocation = new ShellItem(pidlFolder) });

            return HRESULT.S_OK;
        }

        #endregion ============================================================================================================

        #region Implemented Interface: ICommDlgBrowser3 =======================================================================

        public HRESULT OnDefaultCommand(Shell32.IShellView ppshv)
        {
            return HRESULT.S_FALSE;
        }

        public HRESULT OnStateChange(Shell32.IShellView ppshv, Shell32.CDBOSC uChange)
        {
            switch (uChange)
            {
                //case Shell32.CDBOSC.CDBOSC_SETFOCUS:
                //    break;
                //case Shell32.CDBOSC.CDBOSC_KILLFOCUS:
                //    break;
                case Shell32.CDBOSC.CDBOSC_SELCHANGE:
                    this.OnSelectionChanged();
                    break;
                //case Shell32.CDBOSC.CDBOSC_RENAME:
                //    break;
                //case Shell32.CDBOSC.CDBOSC_STATECHANGE:
                //    break;
                default:
                    break;
            }

            return HRESULT.S_OK;
        }

        public HRESULT IncludeObject(Shell32.IShellView ppshv, IntPtr pidl)
        {
            // this.OnIncludeObject(); // TODO: When returning false, the item won't get displayed!
            // See CDB2GVF_NOINCLUDEITEM for this!
            return HRESULT.S_OK;
        }

        public HRESULT GetDefaultMenuText(Shell32.IShellView ppshv, StringBuilder pszText, int cchMax)
        {
            return HRESULT.S_FALSE;
        }

        public HRESULT GetViewFlags(out Shell32.CDB2GVF pdwFlags)
        {
            pdwFlags = Shell32.CDB2GVF.CDB2GVF_SHOWALLFILES;

            return HRESULT.S_OK;
        }

        public HRESULT Notify(Shell32.IShellView ppshv, Shell32.CDB2N dwNotifyType)
        {
            return HRESULT.S_OK;
        }

        public HRESULT GetCurrentFilter(StringBuilder pszFileSpec, int cchFileSpec)
        {
            return HRESULT.S_OK;
        }

        public HRESULT OnColumnClicked(Shell32.IShellView ppshv, int iColumn)
        {
            return HRESULT.S_OK;
        }

        public HRESULT OnPreViewCreated(Shell32.IShellView ppshv)
        {
            return HRESULT.S_OK;
        }

        #endregion ============================================================================================================



        #region Nested class ExplorerBrowserControl.ViewEvents ================================================================

        /// <summary>
        /// 
        /// ExplorerBrowserControl.ViewEvents (Nested class)
        /// 
        /// This nested  class acts as the connection between IShellView hosted by ExplorerBrowser
        /// and ExplorerBrowserControl as its container.
        /// 
        /// It is responsible for forwarding events when IShellView is navigating.
        /// 
        /// </summary>
        [ComVisible(true)]
        [ClassInterface(ClassInterfaceType.AutoDual)]
        public class ViewEvents : IDisposable
        {
            #region Fields ====================================================================================================

            private readonly ExplorerBrowserControl container;

            private object viewDispatch;
            private uint viewConnectionPointCookie;

            /// <summary>
            /// View event dispatch IDs. Pulled from from reference implementation.
            /// 
            /// As far as i can see these Windows Shell-Dispatch IDs are undocumented.
            /// 
            /// <seealso href="https://github.com/aybe/Windows-API-Code-Pack-1.1/blob/master/source/WindowsAPICodePack/Shell/ExplorerBrowser/ExplorerBrowserViewEvents.cs"/>
            /// </summary>
            private static class ExplorerBrowserViewDispatchIds
            {
                internal const int ContentsChanged = 207;
                internal const int FileListEnumDone = 201;
                internal const int SelectionChanged = 200;
                internal const int SelectedItemModified = 220;
            }

            protected static Guid IID_IDispatch = new Guid("00020400-0000-0000-c000-000000000046");         // TODO: Use Vanara
            protected static Guid IID_DShellFolderViewEvents = new Guid("62112aa2-ebe4-11cf-a5fb-0020afe7292d");    // TODO: Use Vanara

            #endregion ========================================================================================================

            internal ViewEvents(ExplorerBrowserControl container)
            {
                this.container = container ??
                    throw new ArgumentNullException(nameof(container));
            }

            internal void ConnectShellView(Shell32.IShellView psv)
            {
                this.DisconnectShellView();

                this.viewDispatch = psv.GetItemObject(Shell32.SVGIO.SVGIO_BACKGROUND, ViewEvents.IID_IDispatch);

                // Use discard here, cause we don't need ppcpOut
                HRESULT hResult = ShlwApi.ConnectToConnectionPoint(this, ViewEvents.IID_DShellFolderViewEvents, true,
                    this.viewDispatch, ref this.viewConnectionPointCookie, out _);

                if (hResult.Failed)
                {
                    Marshal.ReleaseComObject(this.viewDispatch);
                    this.viewDispatch = null;

                    throw new COMException("ExplorerBrowserControl.ViewEvents.ConnectShellView: ShlwApi.ConnectToConnectionPoint() failed.", (int)hResult);
                }
            }

            internal void DisconnectShellView()
            {
                if (this.viewDispatch != null)
                {
                    // Use discard here, cause we don't need ppcpOut
                    ShlwApi.ConnectToConnectionPoint(this, ViewEvents.IID_DShellFolderViewEvents, false,
                        this.viewDispatch, ref this.viewConnectionPointCookie, out _);
                    this.viewConnectionPointCookie = 0;

                    Marshal.ReleaseComObject(this.viewDispatch);
                    this.viewDispatch = null;
                }
            }

            #region IDispatch event handlers ==================================================================================

            /// <summary>
            /// Items collection in ShellView has changed.
            /// 
            /// Note: Needs to be public to be accessible via AutoDual reflection
            /// </summary>
            [DispId(ExplorerBrowserViewDispatchIds.ContentsChanged)]
            public void ViewContentsChanged()
            {
                this.container.OnItemsChanged();
            }

            /// <summary>
            /// ShellView has finished enumerating files.
            /// 
            /// Note: Needs to be public to be accessible via AutoDual reflection
            /// </summary>
            [DispId(ExplorerBrowserViewDispatchIds.FileListEnumDone)]
            public void ViewFileListEnumDone()
            {
                this.container.OnItemsEnumerated();
            }

            /// <summary>
            /// SelectedItems collection in ShellView has changed.
            /// 
            /// Note: Needs to be public to be accessible via AutoDual reflection
            /// </summary>
            [DispId(ExplorerBrowserViewDispatchIds.SelectionChanged)]
            public void ViewSelectionChanged()
            {
                this.container.OnSelectionChanged();
            }

            /// <summary>
            /// The selected item in the ShellView has changed, e.g. a rename.
            /// 
            /// Note: Needs to be public to be accessible via AutoDual reflection
            /// </summary>
            [DispId(ExplorerBrowserViewDispatchIds.SelectedItemModified)]
            public void ViewSelectedItemModified()
            {
                this.container.OnSelectedItemModified();
            }

            #endregion ========================================================================================================

            #region IDisposable Support =======================================================================================

            private bool disposedValue = false; // To detect redundant calls

            protected virtual void Dispose(bool disposing)
            {
                if (!this.disposedValue)
                {
                    if (disposing)
                    {
                        this.DisconnectShellView();
                    }

                    this.viewConnectionPointCookie = 0;
                    this.viewDispatch = null;

                    this.disposedValue = true;
                }
            }

            ~ViewEvents()
            {
                // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
                this.Dispose(false);
            }

            // This code added to correctly implement the disposable pattern.
            public void Dispose()
            {
                // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
                this.Dispose(true);
                System.GC.SuppressFinalize(this);
            }

            #endregion ========================================================================================================
        }

        #endregion ============================================================================================================

        #region Nested class ExplorerBrowserControl.NavigationLog =============================================================

        public class NavigationLog
        {
            private readonly ExplorerBrowserControl parent = null;

            /// <summary>
            /// The pending navigation log action. Null if the user is not navigating via the navigation log.
            /// </summary>
            private PendingNavigation pendingNavigation;

            internal NavigationLog(ExplorerBrowserControl parent)
            {
                this.parent = parent ??
                    throw new ArgumentNullException(nameof(parent));

                this.parent.Navigated += this.OnNavigated;
                this.parent.NavigationFailed += this.OnNavigationFailed;
            }

            /// <summary>
            /// Fires when the navigation log changes or the current navigation position changes
            /// </summary>
            public event EventHandler<NavigationLogEventArgs> NavigationLogChanged;

            /// <summary>
            /// Indicates the presence of locations in the log that can be reached by calling Navigate(Backward)
            /// </summary>
            public bool CanNavigateBackward => this.CurrentLocationIndex > 0;

            /// <summary>
            /// Indicates the presence of locations in the log that can be reached by calling Navigate(Forward)
            /// </summary>
            public bool CanNavigateForward => this.CurrentLocationIndex < this.Locations.Count - 1;

            /// <summary>
            /// Gets the shell object in the Locations collection pointed to by CurrentLocationIndex.
            /// </summary>
            public ShellItem CurrentLocation => this.CurrentLocationIndex < 0 ? null : this.Locations[this.CurrentLocationIndex];

            /// <summary>
            /// An index into the Locations collection. The ShellItem pointed to by this index is the current location of the ExplorerBrowser.
            /// </summary>
            public int CurrentLocationIndex { get; set; } = -1;

            /// <summary>
            /// The navigation log
            /// </summary>
            public List<ShellItem> Locations { get; } = new List<ShellItem>();

            /// <summary>
            /// Clears the contents of the navigation log.
            /// </summary>
            public void Clear()
            {
                if (0 == this.Locations.Count)
                    return;

                var oldCanNavigateBackward = this.CanNavigateBackward;
                var oldCanNavigateForward = this.CanNavigateForward;

                this.Locations.Clear();
                this.CurrentLocationIndex = -1;

                var eventArgs = new NavigationLogEventArgs
                {
                    LocationsChanged = true,
                    CanNavigateBackwardChanged = oldCanNavigateBackward != this.CanNavigateBackward,
                    CanNavigateForwardChanged = oldCanNavigateForward != this.CanNavigateForward
                };

                this.NavigationLogChanged?.Invoke(this, eventArgs);
            }

            internal bool NavigateLog(NavigationLogDirection direction)
            {
                int locationIndex;

                // Determine proper index to navigate to
                if (direction == NavigationLogDirection.Backward && this.CanNavigateBackward)
                {
                    locationIndex = this.CurrentLocationIndex - 1;
                }
                else if (direction == NavigationLogDirection.Forward && this.CanNavigateForward)
                {
                    locationIndex = this.CurrentLocationIndex + 1;
                }
                else
                {
                    return false;
                }

                // Initiate traversal request
                var location = this.Locations[locationIndex];
                this.pendingNavigation = new PendingNavigation(location, locationIndex);
                this.parent.NavigateTo(location);

                return true;
            }

            /// <summary>
            /// Initiate navigation within the navigation log...
            /// </summary>
            /// <param name="index"></param>
            /// <returns>true if successful, false otherwise</returns>
            internal bool NavigateLog(int index)
            {
                // Can't go anywhere
                if ((index >= this.Locations.Count) || (index < 0))
                    return false;

                // No need to re navigate to the same location
                if (index == this.CurrentLocationIndex)
                    return false;

                // Initiate traversal request
                var location = this.Locations[index];
                this.pendingNavigation = new PendingNavigation(location, index);
                this.parent.NavigateTo(location);

                return true;
            }

            private void OnNavigated(object sender, NavigatedEventArgs args)
            {
                var eventArgs = new NavigationLogEventArgs();
                var oldCanNavigateBackward = this.CanNavigateBackward;
                var oldCanNavigateForward = this.CanNavigateForward;

                if (this.pendingNavigation != null)
                {
                    // Navigation log traversal in progress

                    // Determine if new location is the same as the traversal request
                    if (this.pendingNavigation.Location.IShellItem.Compare(
                        args.NewLocation.IShellItem, Shell32.SICHINTF.SICHINT_ALLFIELDS) != 0)
                    {
                        // New location is different than traversal request, behave is if it never happened!
                        // Remove history following CurrentLocationIndex, append new item
                        if (this.CurrentLocationIndex < this.Locations.Count - 1)
                        {
                            this.Locations.RemoveRange(this.CurrentLocationIndex + 1,
                                (this.Locations.Count - (this.CurrentLocationIndex + 1)));
                        }
                        this.Locations.Add(args.NewLocation);
                        this.CurrentLocationIndex = this.Locations.Count - 1;
                        eventArgs.LocationsChanged = true;
                    }
                    else
                    {
                        // Log traversal successful, update index
                        this.CurrentLocationIndex = this.pendingNavigation.Index;
                        eventArgs.LocationsChanged = false;
                    }

                    this.pendingNavigation = null;
                }
                else
                {
                    // Remove history following currentLocationIndex, append new item
                    if (this.CurrentLocationIndex < this.Locations.Count - 1)
                    {
                        this.Locations.RemoveRange(this.CurrentLocationIndex + 1,
                            (this.Locations.Count - (this.CurrentLocationIndex + 1)));
                    }
                    this.Locations.Add(args.NewLocation);
                    this.CurrentLocationIndex = this.Locations.Count - 1;
                    eventArgs.LocationsChanged = true;
                }

                // Update event args
                eventArgs.CanNavigateBackwardChanged = (oldCanNavigateBackward != this.CanNavigateBackward);
                eventArgs.CanNavigateForwardChanged = (oldCanNavigateForward != this.CanNavigateForward);

                // Fire event NavigationLogChanged
                this.NavigationLogChanged?.Invoke(this, eventArgs);
            }

            private void OnNavigationFailed(object sender, NavigationFailedEventArgs args)
            {
                this.pendingNavigation = null;
            }

            /// <summary>A navigation traversal request</summary>
            private class PendingNavigation
            {
                internal PendingNavigation(ShellItem location, int index)
                {
                    this.Location = location;
                    this.Index = index;
                }

                internal int Index { get; set; }

                internal ShellItem Location { get; set; }
            }
        }

        #endregion ============================================================================================================

        #region Nested class ExplorerBrowserControl.ShellItemCollection =======================================================

        /// <summary>
        /// Represents a collection of <see cref="ShellItem"/> attached to an <see cref="ExplorerBrowserControl"/>.
        /// </summary>
        private class ShellItemCollection
          : IReadOnlyList<ShellItem>
        {
            private readonly ExplorerBrowserControl parentExplorerBrowser;
            private readonly Shell32.SVGIO collectionOption;

            internal ShellItemCollection(ExplorerBrowserControl parentExplorerBrowser, Shell32.SVGIO collectionOption)
            {
                this.parentExplorerBrowser = parentExplorerBrowser;
                this.collectionOption = collectionOption;
            }

            /// <summary>Gets the number of elements in the collection.</summary>
            /// <value>Returns a <see cref="int"/> value.</value>
            public int Count
            {
                get
                {
                    var array = this.Array;

                    return (array is null ? 0 : (int)array.GetCount());
                }
            }

            private Shell32.IShellItemArray Array => this.parentExplorerBrowser.GetItemsArray(this.collectionOption);

            private IEnumerable<Shell32.IShellItem> Items
            {
                get
                {
                    var array = this.Array;

                    if (array is null)
                        yield break;

                    for (uint i = 0; i < array.GetCount(); i++)
                        yield return array.GetItemAt(i);
                }
            }

            /// <summary>Gets the <see cref="ShellItem"/> at the specified index.</summary>
            /// <value>The <see cref="ShellItem"/>.</value>
            /// <param name="index">The zero-based index of the element to get.</param>
            public ShellItem this[int index]
            {
                get
                {
                    try
                    {
                        return ShellItem.Open(this.Array.GetItemAt((uint)index));
                    }
                    catch
                    {
                        return null;
                    }
                }
            }

            /// <summary>Returns an enumerator that iterates through the collection.</summary>
            /// <returns>An enumerator that can be used to iterate through the collection.</returns>
            public IEnumerator<ShellItem> GetEnumerator() => this.Items.Select(ShellItem.Open).GetEnumerator();

            /// <summary>Returns an enumerator that iterates through the collection.</summary>
            /// <returns>An enumerator that can be used to iterate through the collection.</returns>
            IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();
        }

        #endregion ============================================================================================================

    }
}
