﻿using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace electrifier.Contracts.Services;

public interface INavigationService
{
    event NavigatedEventHandler Navigated;
    //  TODO:    event DisplayTitleEventHandler

    bool CanGoBack
    {
        get;
    }

    Frame? Frame
    {
        get; set;
    }

    bool NavigateTo(string pageKey, object? parameter = null, bool clearNavigation = false);

    bool GoBack();

    string? PaneDisplayTitle
    {
        get;
    }

    void SetListDataItemForNextConnectedAnimation(object item);
}