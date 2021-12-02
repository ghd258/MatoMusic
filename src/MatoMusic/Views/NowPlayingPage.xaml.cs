﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Threading.Tasks;
using Abp.Configuration;
using Abp.Dependency;
using Abp.Localization;
using MatoMusic.Common;
using MatoMusic.Core;
using MatoMusic.Core.Helper;
using MatoMusic.Core.Localization;
using MatoMusic.Core.Models;
using MatoMusic.Core.Settings;
using MatoMusic.Core.ViewModel;
using MatoMusic.ViewModels;
using Microsoft.Maui.Controls;

namespace MatoMusic
{
    public partial class NowPlayingPage : ContentPage, ITransientDependency
    {
        public IMusicInfoManager MusicInfoManager => DependencyService.Get<IMusicInfoManager>();

        private MusicFunctionPage _musicFunctionPage;
        private PlaylistChoosePage _playlistChoosePage;
        private readonly ISettingManager settingManager;
        private readonly ILocalizationManager localizationManager;

        private INavigation PopupNavigation => Application.Current.MainPage.Navigation;

        public NowPlayingPage(NowPlayingPageViewModel nowPlayingPageViewModel, ISettingManager settingManager, ILocalizationManager localizationManager
)
        {
            InitializeComponent();
            this.Disappearing += NowPlayingPage_Disappearing;
            this.SizeChanged += NowPlayingPage_SizeChanged;
            this.Appearing += NowPlayingPage_Appearing;
            this.BindingContext = nowPlayingPageViewModel;
            this.settingManager = settingManager;
            this.localizationManager = localizationManager;
        }

        private void NowPlayingPage_Appearing(object sender, EventArgs e)
        {
            var isHideQueueButton = settingManager.GetSettingValueForApplication<bool>(CommonSettingNames.IsHideQueueButton);
            this.QueueControlLayout.IsVisible = !isHideQueueButton;
        }


        private async void NowPlayingPage_SizeChanged(object sender, EventArgs e)
        {
            await Task.Delay(500);
            if (this.Width > 0
                && this.Height > 0
                && PreAlbumArt.Width > 0
                && PreAlbumArt.Height > 0)
            {

                Debug.WriteLine("W:" + this.Width);
                Debug.WriteLine("H:" + this.Height);

                InitPreAlbumArtEdgeThickness();
                InitNextAlbumArtEdgeThickness();
            }


        }

        private void InitNextAlbumArtEdgeThickness()
        {
            int edgeThickness = 22;
            var nextRefwidth = Math.Min(NextAlbumArt.Width, NextAlbumArt.Height);
            var nextTransWidth = (this.Width + nextRefwidth) / 2 - edgeThickness;
            this.NextAlbumArt.TranslateTo(nextTransWidth, this.NextAlbumArt.Y);
            //this.NextAlbumArt.TranslationX = nextTransWidth;
        }

        private void InitPreAlbumArtEdgeThickness()
        {
            var edgeThickness = 22;
            var preRefwidth = Math.Min(PreAlbumArt.Width, PreAlbumArt.Height);
            var preTransWidth = -(this.Width + preRefwidth) / 2 + edgeThickness;
            this.PreAlbumArt.TranslateTo(preTransWidth, this.PreAlbumArt.Y);
            //mthis.PreAlbumArt.TranslationX = preTransWidth;
        }


        private void NowPlayingPage_Disappearing(object sender, EventArgs e)
        {
            var viewModel = BindingContext as NowPlayingPageViewModel;
            if (viewModel != null)
                viewModel.IsLrcPanel = false;
        }

        private void OnValueChanged(object sender, ValueChangedEventArgs e)
        {
            var bindableObject = sender as BindableObject;
            if (bindableObject != null)
            {
                var musicRelatedViewModel = bindableObject.BindingContext as MusicRelatedViewModel;
                if (musicRelatedViewModel != null)
                    musicRelatedViewModel.ChangeProgess(e.NewValue);
            }
        }

        private void Button_OnClicked(object sender, EventArgs e)
        {
            CommonHelper.GoPage("QueuePage");
        }


        private void MoreButton_OnClicked(object sender, EventArgs e)
        {


            var imageButton = sender as BindableObject;
            if (!(imageButton.BindingContext as MusicRelatedViewModel).CanPlayExcute(null))
            {
                return;
            }
            var musicInfo = (imageButton.BindingContext as MusicRelatedViewModel).CurrentMusic;
            var mainMenuCellInfos = new List<MenuCellInfo>()
            {
                new MenuCellInfo() {Title = "添加到..", Code = "AddToPlaylist", Icon = "addto"},
                new MenuCellInfo()
                {
                    Title = (musicInfo as MusicInfo).Artist,
                    Code = "GoArtistPage",
                    Icon = "microphone2"
                },
                new MenuCellInfo()
                {
                    Title = (musicInfo as MusicInfo).AlbumTitle,
                    Code = "GoAlbumPage",
                    Icon = "cd2"
                },


            };
            _musicFunctionPage = new MusicFunctionPage(musicInfo, mainMenuCellInfos);
            _musicFunctionPage.OnFinished += MusicFunctionPage_OnFinished;

            PopupNavigation.PushAsync(_musicFunctionPage);

        }

        private async void MusicFunctionPage_OnFinished(object sender, MusicFunctionEventArgs e)
        {
            if (e.MusicInfo == null)
            {
                return;
            }
            await PopupNavigation.PopToRootAsync();
            if (e.MenuCellInfo.Code == "AddToPlaylist")
            {
                _playlistChoosePage = new PlaylistChoosePage();
                _playlistChoosePage.OnFinished += async (o, c) =>
                {
                    if (c != null)
                    {
                        var result = await MusicInfoManager.CreatePlaylistEntry(e.MusicInfo as MusicInfo, c.Id);
                        if (result)
                        {
                            CommonHelper.ShowMsg(string.Format("{0}{1}", localizationManager.GetString(MatoMusicConsts.LocalizationSourceName, "Msg_HasAdded"), c.Title));
                        }
                        else
                        {
                            CommonHelper.ShowMsg(localizationManager.GetString(MatoMusicConsts.LocalizationSourceName, "Msg_AddFaild"));
                        }
                    }
                   await PopupNavigation.PopAsync();
                };
                await PopupNavigation.PushAsync(_playlistChoosePage);

            }

            else if (e.MenuCellInfo.Code == "GoAlbumPage")
            {
                List<AlbumInfo> list;
                var isSucc = await MusicInfoManager.GetAlbumInfos();
                if (!isSucc.IsSucess)
                {
                    CommonHelper.ShowNoAuthorized();
                }
                list = isSucc.Result;
                var albumInfo = list.Find(c => c.Title == (e.MusicInfo as MusicInfo).AlbumTitle);
                CommonHelper.GoNavigate("MusicCollectionPage", new object[] { albumInfo });
            }
            else if (e.MenuCellInfo.Code == "GoArtistPage")
            {
                List<ArtistInfo> list;
                var isSucc = await MusicInfoManager.GetArtistInfos();
                if (!isSucc.IsSucess)
                {
                    CommonHelper.ShowNoAuthorized();

                }
                list = isSucc.Result;
                var artistInfo = list.Find(c => c.Title == (e.MusicInfo as MusicInfo).Artist);
                CommonHelper.GoNavigate("MusicCollectionPage", new object[] { artistInfo });
            }

        }

        private void LyricView_OnOnClosed(object sender, EventArgs e)
        {
            var nowPlayingPageViewModel = this.BindingContext as NowPlayingPageViewModel;
            if (nowPlayingPageViewModel != null)
                nowPlayingPageViewModel.IsLrcPanel = !nowPlayingPageViewModel.IsLrcPanel;
        }

        private void GoLibrary_OnClicked(object sender, EventArgs e)
        {
            CommonHelper.GoPage("LibraryPage");
        }

        private void BindableObject_OnPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "IsVisible")
            {
            }
        }

    }
}
