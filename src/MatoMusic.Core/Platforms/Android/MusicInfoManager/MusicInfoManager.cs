﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Abp.Dependency;
using Abp.Domain.Repositories;
using Abp.Domain.Uow;
using Abp.EntityFrameworkCore.Repositories;
using Android.Database;
using Android.Provider;
using MatoMusic.Core.Helper;
using MatoMusic.Core.Models;
using MatoMusic.Core.ViewModel;
using MatoMusic.Infrastructure;
using Microsoft.International.Converters.PinYinConverter;
using Microsoft.Maui.Controls;
using Application = Android.App.Application;

namespace MatoMusic.Core
{
    public partial class MusicInfoManager : IMusicInfoManager
    {
        private static string[] _mediaProjections =
     {
            MediaStore.Audio.Media.InterfaceConsts.Id,
            MediaStore.Audio.Media.InterfaceConsts.Artist,
            MediaStore.Audio.Media.InterfaceConsts.Album,
            MediaStore.Audio.Media.InterfaceConsts.Title,
            MediaStore.Audio.Media.InterfaceConsts.Duration,
            MediaStore.Audio.Media.InterfaceConsts.Data,
            MediaStore.Audio.Media.InterfaceConsts.IsMusic,
            MediaStore.Audio.Media.InterfaceConsts.AlbumId
        };

        private static string[] _genresProjections =
        {
            MediaStore.Audio.Genres.InterfaceConsts.Name,
            MediaStore.Audio.Genres.InterfaceConsts.Id
        };

        private static string[] _albumProjections =
        {
            MediaStore.Audio.Albums.InterfaceConsts.Id,
            MediaStore.Audio.Albums.InterfaceConsts.AlbumArt
        };
        public IList<MusicInfo> GetAllSongs()
        {

            IList<MusicInfo> songs = new ObservableCollection<MusicInfo>();
            ICursor mediaCursor, genreCursor, albumCursor;

            mediaCursor = Application.Context.ContentResolver.Query(
                MediaStore.Audio.Media.ExternalContentUri,
                _mediaProjections, null, null,
                MediaStore.Audio.Media.InterfaceConsts.TitleKey);

            int artistColumn = mediaCursor.GetColumnIndex(MediaStore.Audio.Media.InterfaceConsts.Artist);
            int albumColumn = mediaCursor.GetColumnIndex(MediaStore.Audio.Media.InterfaceConsts.Album);
            int titleColumn = mediaCursor.GetColumnIndex(MediaStore.Audio.Media.InterfaceConsts.Title);
            int durationColumn = mediaCursor.GetColumnIndex(MediaStore.Audio.Media.InterfaceConsts.Duration);
            int uriColumn = mediaCursor.GetColumnIndex(MediaStore.Audio.Media.InterfaceConsts.Data);
            int idColumn = mediaCursor.GetColumnIndex(MediaStore.Audio.Media.InterfaceConsts.Id);
            int isMusicColumn = mediaCursor.GetColumnIndex(MediaStore.Audio.Media.InterfaceConsts.IsMusic);
            int albumIdColumn = mediaCursor.GetColumnIndex(MediaStore.Audio.Media.InterfaceConsts.AlbumId);

            int isMusic;
            ulong duration, id;
            string artist, album, title, uri, genre, artwork, artworkId;

            if (mediaCursor.MoveToFirst())
            {
                do
                {
                    isMusic = int.Parse(mediaCursor.GetString(isMusicColumn));
                    if (isMusic != 0)
                    {
                        artist = mediaCursor.GetString(artistColumn);
                        album = mediaCursor.GetString(albumColumn);
                        title = mediaCursor.GetString(titleColumn);
                        duration = ulong.Parse(mediaCursor.GetString(durationColumn));
                        uri = mediaCursor.GetString(uriColumn);
                        id = ulong.Parse(mediaCursor.GetString(idColumn));
                        artworkId = mediaCursor.GetString(albumIdColumn);

                        genreCursor = Application.Context.ContentResolver.Query(
                            MediaStore.Audio.Genres.GetContentUriForAudioId("external", (int)id),
                            _genresProjections, null, null, null);
                        int genreColumn = genreCursor.GetColumnIndex(MediaStore.Audio.Genres.InterfaceConsts.Name);
                        if (genreCursor.MoveToFirst())
                        {
                            genre = genreCursor.GetString(genreColumn) ?? string.Empty;
                        }
                        else
                        {
                            genre = string.Empty;
                        }

                        albumCursor = Application.Context.ContentResolver.Query(
                            MediaStore.Audio.Albums.ExternalContentUri,
                            _albumProjections,
                            $"{MediaStore.Audio.Albums.InterfaceConsts.Id}=?",
                            new string[] { artworkId },
                            null);
                        int artworkColumn = albumCursor.GetColumnIndex(MediaStore.Audio.Media.InterfaceConsts.AlbumArt);
                        if (albumCursor.MoveToFirst())
                        {
                            artwork = albumCursor.GetString(artworkColumn) ?? string.Empty;
                        }
                        else
                        {
                            artwork = string.Empty;
                        }

                        songs.Add(new MusicInfo()
                        {
                            Id = (int)id,
                            Title = title,
                            Artist = artist,
                            AlbumTitle = album,
                            Genre = genre,
                            Duration = duration / 1000,
                            Url = uri,
                            AlbumArt = artwork
                        });
                        genreCursor?.Close();
                        albumCursor?.Close();
                    }
                } while (mediaCursor.MoveToNext());
            }
            mediaCursor?.Close();

            return songs;

        }


        private const int MyFavouriteIndex = 1;
        private readonly IRepository<Queue, long> queueRepository;
        private readonly IRepository<PlaylistItem, long> playlistItemRepository;
        private readonly IRepository<Playlist, long> playlistRepository;
        private readonly IUnitOfWorkManager unitOfWorkManager;
        private readonly IMusicSystem _musicSystem;
        private readonly MusicRelatedViewModel musicRelatedViewModel;
        List<MusicInfo> _musicInfos;

        public MusicInfoManager(IRepository<Queue, long> queueRepository,
            IRepository<PlaylistItem, long> playlistItemRepository,
            IRepository<Playlist, long> playlistRepository,
            IUnitOfWorkManager unitOfWorkManager,
            
            MusicRelatedViewModel musicRelatedViewModel
            )
        {
            this.queueRepository = queueRepository;
            this.playlistItemRepository = playlistItemRepository;
            this.playlistRepository = playlistRepository;
            this.unitOfWorkManager = unitOfWorkManager;
            _musicSystem = DependencyService.Get<IMusicSystem>();
            this.musicRelatedViewModel = musicRelatedViewModel;
        }



        /// <summary>
        /// 获取分组包装好的MusicInfo集合
        /// </summary>
        /// <returns></returns>
        public partial async Task<AlphaGroupedObservableCollection<MusicInfo>> GetAlphaGroupedMusicInfo()
        {
            AlphaGroupedObservableCollection<MusicInfo> result = new AlphaGroupedObservableCollection<MusicInfo>();
            List<MusicInfo> list;
            var isSucc = await GetMusicInfos();
            if (!isSucc.IsSucess)
            {
                CommonHelper.ShowNoAuthorized();

            }
            list = isSucc.Result;

            list.ForEach(c =>
            {
                result.Add(c, c.GroupHeader);

            });
            result.Root = result.Root.Where(c => c.HasItems).ToList();
            return result;


        }
        /// <summary>
        /// 获取分组包装好的ArtistInfo集合
        /// </summary>
        /// <returns></returns>
        public partial async Task<AlphaGroupedObservableCollection<ArtistInfo>> GetAlphaGroupedArtistInfo()
        {
            AlphaGroupedObservableCollection<ArtistInfo> result = new AlphaGroupedObservableCollection<ArtistInfo>();
            List<ArtistInfo> list;
            var isSucc = await GetArtistInfos();
            if (!isSucc.IsSucess)
            {
                //CommonHelper.ShowNoAuthorized();

            }
            list = isSucc.Result;
            list.ForEach(c =>
            {
                result.Add(c, c.GroupHeader);

            });
            result.Root = result.Root.Where(c => c.HasItems).ToList();
            return result;


        }

        /// <summary>
        /// 获取分组包装好的AlbumInfo集合
        /// </summary>
        /// <returns></returns>
        public partial async Task<AlphaGroupedObservableCollection<AlbumInfo>> GetAlphaGroupedAlbumInfo()
        {
            AlphaGroupedObservableCollection<AlbumInfo> result = new AlphaGroupedObservableCollection<AlbumInfo>();
            List<AlbumInfo> list;
            var isSucc = await GetAlbumInfos();
            if (!isSucc.IsSucess)
            {
                //CommonHelper.ShowNoAuthorized();

            }
            list = isSucc.Result;
            list.ForEach(c =>
            {
                result.Add(c, c.GroupHeader);

            });
            result.Root = result.Root.Where(c => c.HasItems).ToList();
            return result;


        }

        private bool MediaLibraryAuthorization()
        {
            var result = true;
            return result;

        }

        /// <summary>
        /// 获取MusicInfo集合
        /// </summary>
        /// <returns></returns>
        public partial async Task<InfoResult<List<MusicInfo>>> GetMusicInfos()
        {
            List<MusicInfo> musicInfos;

            var result = false;

            if (MediaLibraryAuthorization())
            {

                musicInfos = await Task.Run(() =>
                {
                    var Infos = (from item in GetAllSongs()

                                 select new MusicInfo()
                                 {
                                     Id = item.Id,
                                     Title = item.Title,
                                     Duration = item.Duration,
                                     Url = item.Url,
                                     AlbumTitle = item.AlbumTitle,
                                     Artist = item.Artist,
                                     AlbumArt = item.AlbumArt,
                                     GroupHeader = GetGroupHeader(item.Title),
                                     IsFavourite = GetIsMyFavouriteContains(item.Title).Result,
                                     IsInitFinished = true
                                 }).ToList();
                    return Infos;
                });

                result = true;

            }
            else
            {
                musicInfos = new List<MusicInfo>();
                result = false;
            }
            return new InfoResult<List<MusicInfo>>(result, musicInfos);

        }

        /// <summary>
        /// 获取AlbumInfo集合
        /// </summary>
        /// <returns></returns>

        public partial async Task<InfoResult<List<AlbumInfo>>> GetAlbumInfos()
        {
            List<AlbumInfo> albumInfo;
            var result = false;

            if (MediaLibraryAuthorization())
            {
                var isSucc = await GetMusicInfos();
                if (!isSucc.IsSucess)
                {
                    //CommonHelper.ShowNoAuthorized();

                }
                albumInfo = await Task.Run(() =>
                {
                    var info = (from item in isSucc.Result
                                group item by item.AlbumTitle
                        into c
                                select new AlbumInfo()
                                {
                                    Title = c.Key,
                                    GroupHeader = GetGroupHeader(c.Key),

                                    AlbumArt = c.FirstOrDefault().AlbumArt,
                                    Musics = new ObservableCollection<MusicInfo>(c.Select(d => new MusicInfo()
                                    {
                                        Id = d.Id,
                                        Title = d.Title,
                                        Duration = d.Duration,
                                        Url = d.Url,
                                        AlbumTitle = d.AlbumTitle,
                                        Artist = d.Artist,
                                        AlbumArt = d.AlbumArt,
                                        IsFavourite = GetIsMyFavouriteContains(d.Title).Result,
                                        IsInitFinished = true
                                    }))

                                }).ToList();
                    return info;
                });

                result = true;

            }
            else
            {
                albumInfo = new List<AlbumInfo>();
                result = false;
            }
            return new InfoResult<List<AlbumInfo>>(result, albumInfo);

        }

        /// <summary>
        /// 获取ArtistInfo集合
        /// </summary>
        /// <returns></returns>
        public partial async Task<InfoResult<List<ArtistInfo>>> GetArtistInfos()
        {
            List<ArtistInfo> artistInfo;
            var result = false;
            if (MediaLibraryAuthorization())
            {
                var isSucc = await GetMusicInfos();
                if (!isSucc.IsSucess)
                {
                    //CommonHelper.ShowNoAuthorized();

                }
                artistInfo = await Task.Run(() =>
                {

                    var info = (from item in isSucc.Result
                                group item by item.Artist
                        into c
                                select new ArtistInfo()
                                {
                                    Title = c.Key,
                                    GroupHeader = GetGroupHeader(c.Key),
                                    Musics = new ObservableCollection<MusicInfo>(c.Select(d => new MusicInfo()
                                    {
                                        Id = d.Id,
                                        Title = d.Title,
                                        Duration = d.Duration,
                                        Url = d.Url,
                                        AlbumTitle = d.AlbumTitle,
                                        Artist = d.Artist,
                                        AlbumArt = d.AlbumArt,
                                        IsFavourite = GetIsMyFavouriteContains(d.Title).Result,
                                        IsInitFinished = true

                                    }))

                                }).ToList();
                    return info;
                });
                result = true;

            }
            else
            {
                artistInfo = new List<ArtistInfo>();
                result = false;
            }
            return new InfoResult<List<ArtistInfo>>(result, artistInfo);
        }

        /// <summary>
        /// 将MusicInfo插入到队列
        /// </summary>
        /// <param name="musicInfo">musicInfo对象</param>
        /// <returns></returns>
        public partial async Task<bool> CreateQueueEntry(MusicInfo musicInfo)
        {
            var entry = new Queue(musicInfo.Title, 0, musicInfo.Id);
            var result = await queueRepository.InsertAndGetIdAsync(entry);
            return result > 0;
        }



        /// <summary>
        /// 将MusicInfo集合插入到队列中的末尾
        /// </summary>
        /// <param name="musicInfos"></param>
        /// <returns></returns>
        public partial async Task<bool> InsertToEndQueueEntrys(List<MusicInfo> musicInfos)
        {
            //var rankValue = 0;
            var sortedMusicInfos = musicInfos.Except(await GetQueueEntry(), new MusicInfoComparer()).ToList();
            var result = await CreateQueueEntrys(sortedMusicInfos);
            return result;
        }

        /// <summary>
        /// 将MusicInfo集合插入到队列
        /// </summary>
        /// <param name="musicInfos">需要进行操作的MusicInfo集合</param>
        /// <returns></returns>
        public partial async Task<bool> CreateQueueEntrys(List<MusicInfo> musicInfos)
        {
            var entrys = musicInfos.Select(c => new Queue(c.Title, 0, c.Id));
            await queueRepository.GetDbContext().AddRangeAsync(entrys);
            return true;
        }

        /// <summary>
        /// 将MusicInfo集合插入到队列
        /// </summary>
        /// <param name="musics">需要进行操作的MusicInfo集合</param>
        /// <returns></returns>
        public partial async Task<bool> CreateQueueEntrys(MusicCollectionInfo musics)
        {
            return await CreateQueueEntrys(musics.Musics.ToList());
        }

        /// <summary>
        /// 从队列中读取MusicInfo
        /// </summary>
        /// <returns></returns>
        public partial async Task<List<MusicInfo>> GetQueueEntry()
        {
            var queueEntrys = await queueRepository.GetAllListAsync();
            if (_musicInfos == null || _musicInfos.Count == 0)
            {
                var isSucc = await GetMusicInfos();
                if (!isSucc.IsSucess)
                {
                    //CommonHelper.ShowNoAuthorized();
                }
                _musicInfos = isSucc.Result;

            }
            var result =
                from musicInfo in _musicInfos
                join queue in queueEntrys
                on musicInfo.Id equals queue.MusicInfoId
                orderby queue.Id
                select musicInfo;
            return result.ToList();

        }

        /// <summary>
        /// 将MusicInfo插入到队列中的下一曲
        /// </summary>
        /// <param name="musicInfo">musicInfo对象</param>
        /// <returns></returns>
        public partial async Task<bool> InsertToNextQueueEntry(MusicInfo musicInfo)
        {
            var result = false;
            var isSuccessCreate = false;
            //如果没有则先创建
            if (!await GetIsQueueContains(musicInfo.Title))
            {
                isSuccessCreate = await CreateQueueEntry(musicInfo);
            }
            else
            {
                isSuccessCreate = true;
            }
            //确定包含后与下一曲交换位置
            if (isSuccessCreate)
            {
                var currnet = musicRelatedViewModel.CurrentMusic;
                var next = _musicSystem.GetNextMusic(currnet, false);

                ReorderQueue(musicInfo, next);
                result = true;
            }
            else
            {
                result = false;
            }
            return result;
        }

        /// <summary>
        /// 将MusicInfo插入到队列中的末尾
        /// </summary>
        /// <param name="musicInfo">musicInfo对象</param>
        /// <returns></returns>
        public partial async Task<bool> InsertToEndQueueEntry(MusicInfo musicInfo)
        {
            var result = false;
            var isSuccessCreate = false;
            //如果没有则先创建    
            var queueEntrys = await queueRepository.GetAllListAsync();
            var queueEntry = queueEntrys.FirstOrDefault(c => c.MusicTitle == musicInfo.Title);

            if (queueEntry == null)
            {
                isSuccessCreate = await CreateQueueEntry(musicInfo);
            }
            else
            {
                await DeleteMusicInfoFormQueueEntry(queueEntry.MusicTitle);
                isSuccessCreate = await CreateQueueEntry(musicInfo);
            }

            return isSuccessCreate;
        }


        /// <summary>
        /// 返回一个值表明一个Title是否包含在队列中
        /// </summary>
        /// <param name="musicTitle">music标题</param>
        /// <returns></returns>
        public partial async Task<bool> GetIsQueueContains(string musicTitle)
        {
            var queueEntrys = await queueRepository.FirstOrDefaultAsync(c => c.MusicTitle == musicTitle);
            return queueEntrys is not null;
        }

        /// <summary>
        /// 从队列中删除指定MusicInfo
        /// </summary>
        /// <param name="musicTitle"></param>
        /// <returns></returns>
        public partial async Task<bool> DeleteMusicInfoFormQueueEntry(string musicTitle)
        {
            var entry = await queueRepository.FirstOrDefaultAsync(c => c.MusicTitle == musicTitle);
            if (entry == null) return false;
            await queueRepository.DeleteAsync(entry);
            return true;
        }

        /// <summary>
        /// 从队列中删除指定MusicInfo
        /// </summary>
        /// <param name="musicInfo">musicInfo对象</param>
        /// <returns></returns>
        public partial async Task<bool> DeleteMusicInfoFormQueueEntry(MusicInfo musicInfo)
        {
            var musicTitle = musicInfo.Title;
            var entry = await queueRepository.FirstOrDefaultAsync(c => c.MusicTitle == musicTitle);
            if (entry == null) return false;
            await queueRepository.DeleteAsync(entry);
            return true;
        }

        /// <summary>
        /// 交换队列中两个MusicInfo的位置
        /// </summary>
        /// <param name="oldMusicInfo"></param>
        /// <param name="newMusicInfo"></param>
        public partial void ReorderQueue(MusicInfo oldMusicInfo, MusicInfo newMusicInfo)
        {

        }

        /// <summary>
        /// 从队列中清除所有MusicInfo
        /// </summary>
        public partial async Task ClearQueue()
        {
            await queueRepository.DeleteAsync(c => true);

        }

        /// <summary>
        /// 将MusicInfo插入到歌单
        /// </summary>
        /// <param name="musicInfo">musicInfo对象</param>
        /// <param name="playlistId"></param>
        /// <returns></returns>
        public partial async Task<bool> CreatePlaylistEntry(MusicInfo musicInfo, long playlistId)
        {
            var entry = new PlaylistItem(playlistId, musicInfo.Title, 0);
            var result = await playlistItemRepository.InsertAndGetIdAsync(entry) > 0;
            if (result)
            {
                if (playlistId == MyFavouriteIndex)
                {
                    musicInfo.SetFavourite(true, false);
                }
            }
            return result;
        }

        /// <summary>
        /// 将MusicInfo集合插入到歌单
        /// </summary>
        /// <param name="musics"></param>
        /// <param name="playlistId"></param>
        /// <returns></returns>
        public partial async Task<bool> CreatePlaylistEntrys(List<MusicInfo> musics, long playlistId)
        {
            var entrys = musics.Select(c => new PlaylistItem(playlistId, c.Title, 0));
            await playlistItemRepository.GetDbContext().AddRangeAsync(entrys);

            if (playlistId == MyFavouriteIndex)
            {
                foreach (var musicInfo in musics)
                {
                    musicInfo.SetFavourite(true, false);
                }
            }

            return true;
        }

        /// <summary>
        /// 将MusicInfo集合插入到歌单
        /// </summary>
        /// <param name="musicCollectionInfo"></param>
        /// <param name="playlistId"></param>
        /// <returns></returns>
        public partial async Task<bool> CreatePlaylistEntrys(MusicCollectionInfo musicCollectionInfo, long playlistId)
        {
            var result = await CreatePlaylistEntrys(musicCollectionInfo.Musics.ToList(), playlistId);
            return result;
        }


        /// <summary>
        /// 从歌单中删除MusicInfo根据指定的Title
        /// </summary>
        /// <param name="musicTitle"></param>
        /// <param name="playlistId"></param>
        /// <returns></returns>
        public partial async Task<bool> DeletePlaylistEntry(string musicTitle, long playlistId)
        {
            var entry = await playlistItemRepository.FirstOrDefaultAsync(c => c.PlaylistId == playlistId && c.MusicTitle == musicTitle);
            if (entry != null)
            {
                await playlistItemRepository.DeleteAsync(entry);
                return true;
            }
            else
            {
                return false;
            }
        }

        /// <summary>
        /// 从歌单中删除MusicInfo
        /// </summary>
        /// <param name="musicInfo">musicInfo对象</param>
        /// <param name="playlistId"></param>
        /// <returns></returns>
        public partial async Task<bool> DeletePlaylistEntry(MusicInfo musicInfo, long playlistId)
        {
            var result = await DeletePlaylistEntry(musicInfo.Title, playlistId);
            if (result)
            {
                musicInfo.SetFavourite(false, false);
            }
            return result;
        }


        /// <summary>
        /// 将MusicInfo插入到“我最喜爱”歌单
        /// </summary>
        /// <param name="musicInfo">musicInfo对象</param>
        /// <returns></returns>
        public partial async Task<bool> CreatePlaylistEntryToMyFavourite(MusicInfo musicInfo)
        {
            var result = await CreatePlaylistEntry(musicInfo, MyFavouriteIndex);
            return result;

        }

        /// <summary>
        /// 将MusicInfo集合插入到“我最喜爱”
        /// </summary>
        /// <param name="musics"></param>
        /// <returns></returns>
        public partial async Task<bool> CreatePlaylistEntrysToMyFavourite(List<MusicInfo> musics)
        {
            var result = await CreatePlaylistEntrys(musics, MyFavouriteIndex);
            return result;
        }

        /// <summary>
        /// 将MusicInfo集合插入到“我最喜爱”
        /// </summary>
        /// <param name="musicCollectionInfo"></param>
        /// <returns></returns>
        public partial async Task<bool> CreatePlaylistEntrysToMyFavourite(MusicCollectionInfo musicCollectionInfo)
        {
            var result = await CreatePlaylistEntrys(musicCollectionInfo, MyFavouriteIndex);
            return result;
        }



        /// <summary>
        /// 从“我最喜爱”中删除MusicInfo
        /// </summary>
        /// <param name="musicInfo">musicInfo对象</param>
        /// <returns></returns>
        public partial async Task<bool> DeletePlaylistEntryFromMyFavourite(MusicInfo musicInfo)
        {
            return await DeletePlaylistEntry(musicInfo, MyFavouriteIndex);
        }

        /// <summary>
        /// 从歌单中读取MusicInfo
        /// </summary>
        /// <param name="playlistId"></param>
        /// <returns></returns>
        public partial async Task<List<MusicInfo>> GetPlaylistEntry(long playlistId)
        {
            var currentPlaylistEntrie = await playlistItemRepository.GetAllListAsync(c => c.PlaylistId == playlistId);
            List<MusicInfo> musicInfos;

            var isSucc = await GetMusicInfos();
            if (!isSucc.IsSucess)
            {
                //CommonHelper.ShowNoAuthorized();

            }
            musicInfos = isSucc.Result;

            var result = from item
                in musicInfos
                         where (from c
                             in currentPlaylistEntrie
                                select c.MusicTitle).Contains(item.Title)
                         orderby item.Id
                         select item;
            return result.ToList();

        }

        /// <summary>
        /// 从“我最喜爱”中读取MusicInfo
        /// </summary>
        /// <returns></returns>
        public partial async Task<List<MusicInfo>> GetPlaylistEntryFormMyFavourite()
        {
            return await GetPlaylistEntry(MyFavouriteIndex);
        }

        /// <summary>
        /// 返回一个值表明一个Title是否包含在某个歌单中
        /// </summary>
        /// <param name="musicTitle">music标题</param>
        /// <param name="playlistId"></param>
        /// <returns></returns>
        public partial async Task<bool> GetIsPlaylistContains(string musicTitle, long playlistId)
        {
            var result = await playlistItemRepository.FirstOrDefaultAsync(c => c.MusicTitle == musicTitle && c.PlaylistId == playlistId);
            return result is not null;

        }

        /// <summary>
        ///  返回一个值表明一个MusicInfo是否包含在某个歌单中
        /// </summary>
        /// <param name="musicInfo">musicInfo对象</param>
        /// <param name="playlistId"></param>
        /// <returns></returns>
        public partial async Task<bool> GetIsPlaylistContains(MusicInfo musicInfo, long playlistId)
        {
            return await GetIsPlaylistContains(musicInfo.Title, playlistId);
        }

        /// <summary>
        /// 返回一个值表明一个Title是否包含在"我最喜爱"中
        /// </summary>
        /// <param name="musicTitle">music标题</param>
        /// <returns></returns>
        public partial async Task<bool> GetIsMyFavouriteContains(string musicTitle)
        {
            return await GetIsPlaylistContains(musicTitle, MyFavouriteIndex);

        }

        /// <summary>
        ///  返回一个值表明一个MusicInfo是否包含在"我最喜爱"中
        /// </summary>
        /// <param name="musicInfo">musicInfo对象</param>
        /// <returns></returns>
        public partial async Task<bool> GetIsMyFavouriteContains(MusicInfo musicInfo)
        {
            return await GetIsPlaylistContains(musicInfo, MyFavouriteIndex);

        }

        /// <summary>
        /// 交换某歌单中两个MusicInfo的位置
        /// </summary>
        /// <param name="oldMusicInfo"></param>
        /// <param name="newMusicInfo"></param>
        /// <param name="playlistId"></param>
        public partial void ReorderPlaylist(MusicInfo oldMusicInfo, MusicInfo newMusicInfo, long playlistId)
        {
        }
        /// <summary>
        /// 交换"我最喜爱"中两个MusicInfo的位置
        /// </summary>
        /// <param name="oldMusicInfo"></param>
        /// <param name="newMusicInfo"></param>
        public partial void ReorderMyFavourite(MusicInfo oldMusicInfo, MusicInfo newMusicInfo)
        {
            ReorderPlaylist(oldMusicInfo, newMusicInfo, MyFavouriteIndex);
        }
        /// <summary>
        /// 获取Playlist
        /// </summary>
        /// <returns></returns>
        public partial async Task<List<Playlist>> GetPlaylist()
        {
            return await playlistRepository.GetAllListAsync();
        }


        /// <summary>
        /// 创建Playlist
        /// </summary>
        /// <param name="playlist"></param>
        /// <returns></returns>
        public partial async Task<bool> CreatePlaylist(Playlist playlist)
        {
            var result = await playlistRepository.InsertAndGetIdAsync(playlist);
            return result > 0;
        }

        /// <summary>
        /// 更新Playlist
        /// </summary>
        /// <param name="playlist"></param>
        /// <returns></returns>
        public partial async Task<bool> UpdatePlaylist(Playlist playlist)
        {
            var result = await playlistRepository.InsertOrUpdateAndGetIdAsync(playlist);
            return result > 0;
        }

        /// <summary>
        /// 根据Id删除Playlist
        /// </summary>
        /// <param name="playlistId"></param>
        /// <returns></returns>
        public partial async Task<bool> DeletePlaylist(long playlistId)
        {
            await playlistItemRepository.DeleteAsync(c => c.PlaylistId == playlistId);
            await playlistRepository.DeleteAsync(playlistId);
            return true;
        }

        /// <summary>
        /// 根据Playlist删除Playlist
        /// </summary>
        /// <param name="playlist"></param>
        /// <returns></returns>
        public partial async Task<bool> DeletePlaylist(Playlist playlist)
        {
            return await DeletePlaylist(playlist.Id);

        }
        /// <summary>
        /// 获取一个字符串的标题头
        /// </summary>
        /// <param name="title"></param>
        /// <returns></returns>
        private string GetGroupHeader(string title)
        {
            string result = string.Empty;
            if (!string.IsNullOrEmpty(title))
            {


                if (Regex.IsMatch(title.Substring(0, 1), @"^[\u4e00-\u9fa5]+$"))
                {
                    try
                    {
                        var chinese = new ChineseChar(title.First());
                        result = chinese.Pinyins[0].Substring(0, 1);
                    }
                    catch (Exception ex)
                    {
                        return string.Empty;
                    }

                }
                else
                {
                    result = title.Substring(0, 1);
                }
            }
            return result;

        }

    }
}
