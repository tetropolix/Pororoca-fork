using System.Collections.ObjectModel;
using System.Reactive;
using Pororoca.Desktop.HotKeys;
using Pororoca.Desktop.Localization;
using Pororoca.Domain.Features.Entities.Pororoca;
using Pororoca.Domain.Features.Entities.Pororoca.Http;
using Pororoca.Domain.Features.Entities.Pororoca.WebSockets;
using Pororoca.Domain.Features.VariableResolution;
using ReactiveUI;

namespace Pororoca.Desktop.ViewModels;

public sealed class CollectionFolderViewModel : CollectionOrganizationItemParentViewModel<CollectionOrganizationItemViewModel>
{
    #region COLLECTION ORGANIZATION

    public override Action OnAfterItemDeleted => Parent.OnAfterItemDeleted;
    public override Action<CollectionOrganizationItemViewModel> OnRenameSubItemSelected => Parent.OnRenameSubItemSelected;
    public ReactiveCommand<Unit, Unit> AddNewFolderCmd { get; }
    public ReactiveCommand<Unit, Unit> AddNewHttpRequestCmd { get; }
    public ReactiveCommand<Unit, Unit> AddNewWebSocketConnectionCmd { get; }

    #endregion

    #region COLLECTION FOLDER

    private readonly CollectionViewModel variableResolver;
    public override ObservableCollection<CollectionOrganizationItemViewModel> Items { get; }

    #endregion

    public CollectionFolderViewModel(ICollectionOrganizationItemParentViewModel parentVm,
                                     CollectionViewModel variableResolver,
                                     PororocaCollectionFolder folder) : base(parentVm, folder.Name)
    {
        #region COLLECTION ORGANIZATION

        AddNewFolderCmd = ReactiveCommand.Create(AddNewFolder);
        AddNewHttpRequestCmd = ReactiveCommand.Create(AddNewHttpRequest);
        AddNewWebSocketConnectionCmd = ReactiveCommand.Create(AddNewWebSocketConnection);

        #endregion

        #region COLLECTION FOLDER

        this.variableResolver = variableResolver;

        Items = new();
        foreach (var subFolder in folder.Folders)
            Items.Add(new CollectionFolderViewModel(this, variableResolver, subFolder));
        foreach (var req in folder.Requests)
        {
            if (req is PororocaHttpRequest httpReq)
                Items.Add(new HttpRequestViewModel(this, variableResolver, httpReq));
            else if (req is PororocaWebSocketConnection ws)
                Items.Add(new WebSocketConnectionViewModel(this, variableResolver, ws));
        }

        RefreshSubItemsAvailableMovements();

        #endregion
    }

    #region COLLECTION ORGANIZATION

    public override void RefreshSubItemsAvailableMovements()
    {
        for (int x = 0; x < Items.Count; x++)
        {
            var colItemVm = Items[x];
            int indexOfLastSubfolder = Items.GetLastIndexOf<CollectionFolderViewModel>();
            if (colItemVm is CollectionFolderViewModel)
            {
                colItemVm.CanMoveUp = x > 0;
                colItemVm.CanMoveDown = x < indexOfLastSubfolder;
            }
            else // http requests and websockets
            {
                colItemVm.CanMoveUp = x > (indexOfLastSubfolder + 1);
                colItemVm.CanMoveDown = x < Items.Count - 1;
            }
        }
    }

    protected override void CopyThis() =>
        ClipboardArea.Instance.PushToCopy(ToCollectionFolder());

    public override void PasteToThis()
    {
        var itemsToPaste = ClipboardArea.Instance.FetchCopiesOfFoldersAndReqs();
        foreach (var itemToPaste in itemsToPaste)
        {
            if (itemToPaste is PororocaCollectionFolder folderToPaste)
                AddFolder(folderToPaste);
            else if (itemToPaste is PororocaHttpRequest httpReqToPaste)
                AddHttpRequest(httpReqToPaste);
            else if (itemToPaste is PororocaWebSocketConnection wsToPaste)
                AddWebSocketConnection(wsToPaste);
        }
    }

    private void AddNewFolder()
    {
        PororocaCollectionFolder newFolder = new(Localizer.Instance.Folder.NewFolder);
        AddFolder(newFolder, showItemInScreen: true);
    }

    private void AddNewHttpRequest()
    {
        PororocaHttpRequest newReq = new(Localizer.Instance.HttpRequest.NewRequest);
        AddHttpRequest(newReq, showItemInScreen: true);
    }

    private void AddNewWebSocketConnection()
    {
        PororocaWebSocketConnection newWs = new(Localizer.Instance.WebSocketConnection.NewConnection);
        AddWebSocketConnection(newWs, showItemInScreen: true);
    }

    public void AddFolder(PororocaCollectionFolder folderToAdd, bool showItemInScreen = false)
    {
        CollectionFolderViewModel folderToAddVm = new(this, this.variableResolver, folderToAdd);
        int indexToInsertAt = Items.GetLastIndexOf<CollectionFolderViewModel>() + 1;
        Items.Insert(indexToInsertAt, folderToAddVm);
        
        IsExpanded = true;
        RefreshSubItemsAvailableMovements();
        SetAsItemInFocus(folderToAddVm, showItemInScreen);
    }

    public void AddHttpRequest(PororocaHttpRequest reqToAdd, bool showItemInScreen = false)
    {
        HttpRequestViewModel reqToAddVm = new(this, this.variableResolver, reqToAdd);
        Items.Add(reqToAddVm); // always at the end

        IsExpanded = true;
        RefreshSubItemsAvailableMovements();
        SetAsItemInFocus(reqToAddVm, showItemInScreen);
    }

    public void AddWebSocketConnection(PororocaWebSocketConnection wsToAdd, bool showItemInScreen = false)
    {
        WebSocketConnectionViewModel wsToAddVm = new(this, this.variableResolver, wsToAdd);
        Items.Add(wsToAddVm); // always at the end

        IsExpanded = true;
        RefreshSubItemsAvailableMovements();
        SetAsItemInFocus(wsToAddVm, showItemInScreen);
    }

    #endregion

    #region COLLECTION FOLDER

    public PororocaCollectionFolder ToCollectionFolder()
    {
        PororocaCollectionFolder newFolder = new(Name);
        foreach (var colItemVm in Items)
        {
            if (colItemVm is CollectionFolderViewModel colFolderVm)
                newFolder.AddFolder(colFolderVm.ToCollectionFolder());
            else if (colItemVm is HttpRequestViewModel reqVm)
                newFolder.AddRequest(reqVm.ToHttpRequest());
            else if (colItemVm is WebSocketConnectionViewModel wsVm)
                newFolder.AddRequest(wsVm.ToWebSocketConnection());
        }
        return newFolder;
    }

    #endregion
}