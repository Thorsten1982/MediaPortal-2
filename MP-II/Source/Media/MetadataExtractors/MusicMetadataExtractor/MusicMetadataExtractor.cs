#region Copyright (C) 2007-2008 Team MediaPortal

/*
    Copyright (C) 2007-2008 Team MediaPortal
    http://www.team-mediaportal.com
 
    This file is part of MediaPortal II

    MediaPortal II is free software: you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.

    MediaPortal II is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with MediaPortal II.  If not, see <http://www.gnu.org/licenses/>.
*/

#endregion

using System;
using System.Collections.Generic;
using System.IO;
using MediaPortal.Core;
using MediaPortal.Core.MediaManagement;
using MediaPortal.Core.MediaManagement.DefaultItemAspects;
using MediaPortal.Core.MediaManagement.MediaProviders;
using TagLib;
using TagLib.Id3v2;
using File = TagLib.File;
using Tag = TagLib.Id3v2.Tag;
using MediaPortal.Core.Logging;

namespace MediaPortal.Media.MetadataExtractors.MusicMetadataExtractor
{
  /// <summary>
  /// MediaPortal-II metadata extractor implementation for music files. Supports several formats.
  /// </summary>
  public class MusicMetadataExtractor : IMetadataExtractor
  {
    #region Public constants

    /// <summary>
    /// GUID string for the music metadata extractor.
    /// </summary>
    public const string METADATAEXTRACTOR_ID_STR = "{817FEE2E-8690-4355-9F24-3BDC65AEDFFE}";

    /// <summary>
    /// Music metadata extractor GUID.
    /// </summary>
    public static Guid METADATAEXTRACTOR_ID = new Guid(METADATAEXTRACTOR_ID_STR);

    #endregion

    #region Protected fields and classes

    protected static IList<string> SHARE_CATEGORIES = new List<string>();
    protected static IList<string> AUDIO_EXTENSIONS = new List<string>();

    static MusicMetadataExtractor()
    {
      SHARE_CATEGORIES.Add(DefaultMediaCategory.Audio.ToString());

      AUDIO_EXTENSIONS.Add(".ape");
      AUDIO_EXTENSIONS.Add(".flac");
      AUDIO_EXTENSIONS.Add(".mp3");
      AUDIO_EXTENSIONS.Add(".ogg");
      AUDIO_EXTENSIONS.Add(".wv");
      AUDIO_EXTENSIONS.Add(".wav");
      AUDIO_EXTENSIONS.Add(".wma");
      AUDIO_EXTENSIONS.Add(".mp4");
      AUDIO_EXTENSIONS.Add(".m4a");
      AUDIO_EXTENSIONS.Add(".m4p");
      AUDIO_EXTENSIONS.Add(".mpc");
      AUDIO_EXTENSIONS.Add(".mp+");
      AUDIO_EXTENSIONS.Add(".mpp");
    }

    /// <summary>
    /// Music file accessor class needed for our tag library implementation. This class maps
    /// the TagLib#'s <see cref="File.IFileAbstraction"/> view to an MP-II file from a media provider.
    /// </summary>
    protected class MediaProviderFileAbstraction : File.IFileAbstraction
    {
      protected IMediaProvider _provider;
      protected string _path;

      public MediaProviderFileAbstraction(IMediaProvider provider, string path)
      {
        _provider = provider;
        _path = path;
      }

      #region IFileAbstraction implementation

      public void CloseStream(Stream stream)
      {
        stream.Close();
      }

      public string Name
      {
        get { return _path; }
      }

      public Stream ReadStream
      {
        get { return System.IO.File.OpenRead(_path); }
      }

      public Stream WriteStream
      {
        get { return System.IO.File.OpenWrite(_path); }
      }

      #endregion
    }

    protected MetadataExtractorMetadata _metadata;

    #endregion

    #region Ctor

    public MusicMetadataExtractor()
    {
      _metadata = new MetadataExtractorMetadata(METADATAEXTRACTOR_ID, "Music metadata extractor",
          SHARE_CATEGORIES, new[]
              {
                MediaAspect.Metadata,
                MusicAspect.Metadata
              });
    }

    #endregion

    #region Protected methods

    /// <summary>
    /// Returns the information if the specified file name (or path) has a file extension which is
    /// supposed to be supported by this metadata extractor.
    /// </summary>
    /// <param name="fileName">Relative or absolute file path to check.</param>
    /// <returns><c>true</c>, if the file's extension is supposed to be supported, else <c>false</c>.</returns>
    protected static bool HasAudioExtension(string fileName)
    {
      string ext = Path.GetExtension(fileName).ToLower();
      return AUDIO_EXTENSIONS.Contains(ext);
    }

    /// <summary>
    /// Given a music file name (or path), this method tries to guess the title of the music. This is done
    /// by looking for a '-' character and taking the succeeding part of the name, or the whole name if
    /// no '-' character is present.
    /// </summary>
    /// <param name="filePath">Absolute or relative music file name.</param>
    /// <returns>Guessed title string.</returns>
    protected static string GuessTitle(string filePath)
    {
      string fileName = Path.GetFileName(filePath);
      int i = fileName.IndexOf('-');
      return i == -1 ? Path.GetFileNameWithoutExtension(fileName) : fileName.Substring(i + 1).Trim();
    }

    /// <summary>
    /// Given a music file name (or path), this method tries to guess the artist(s) of the music. This is done
    /// by looking for a '-' character and taking the preceding part of the name. If no '-' character is
    /// present, an empty enumeration is returned.
    /// </summary>
    /// <param name="filePath">Absolute or relative music file name.</param>
    /// <returns>Guessed artist(s) enumeration.</returns>
    protected static IEnumerable<string> GuessArtists(string filePath)
    {
      string fileName = Path.GetFileName(filePath);
      int i = fileName.IndexOf('-');
      if (i > -1)
        yield return fileName.Substring(0, i).Trim();
    }

    #endregion

    #region IMetadataExtractor implementation

    public MetadataExtractorMetadata Metadata
    {
      get { return _metadata; }
    }

    public bool TryExtractMetadata(IMediaProvider provider, string path, IDictionary<Guid, MediaItemAspect> extractedAspectData)
    {
      if (!HasAudioExtension(path))
        return false;

      MediaItemAspect mediaAspect = extractedAspectData[MediaAspect.ASPECT_ID];
      MediaItemAspect musicAspect = extractedAspectData[MusicAspect.ASPECT_ID];
      try
      {
        // Set the flag to use the standard system encoding set by the user
        // Otherwise Latin1 is used as default, which causes characters in various languages being displayed wrong
        ByteVector.UseBrokenLatin1Behavior = true;
        File tag = File.Create(new MediaProviderFileAbstraction(provider, path));

        string title = string.IsNullOrEmpty(tag.Tag.Title) ? GuessTitle(path) : tag.Tag.Title;
        IEnumerable<string> artists = tag.Tag.Performers.Length == 0 ? GuessArtists(path) : tag.Tag.Performers;
        mediaAspect.SetAttribute(MediaAspect.ATTR_TITLE, title);
        musicAspect.SetCollectionAttribute(MusicAspect.ATTR_ARTISTS, artists);
        musicAspect.SetAttribute(MusicAspect.ATTR_ALBUM, tag.Tag.Album);
        musicAspect.SetCollectionAttribute(MusicAspect.ATTR_ALBUMARTISTS, tag.Tag.AlbumArtists);
        musicAspect.SetAttribute(MusicAspect.ATTR_BITRATE, tag.Properties.AudioBitrate);
        mediaAspect.SetAttribute(MediaAspect.ATTR_COMMENT, tag.Tag.Comment);
        musicAspect.SetCollectionAttribute(MusicAspect.ATTR_COMPOSERS, tag.Tag.Composers);
        // The following code gets cover art images - and there is no cover art attribute in any media item aspect
        // defined yet. (Albert78, 2008-11-19)
        //IPicture[] pics = new IPicture[] { };
        //pics = tag.Tag.Pictures;
        //if (pics.Length > 0)
        //{
        //  musictag.CoverArtImageBytes = pics[0].Data.Data;
        //}
        musicAspect.SetAttribute(MusicAspect.ATTR_DURATION, (int) tag.Properties.Duration.TotalSeconds);
        musicAspect.SetCollectionAttribute(MusicAspect.ATTR_GENRES, tag.Tag.Genres);
        musicAspect.SetAttribute(MusicAspect.ATTR_TRACK, (int) tag.Tag.Track);
        musicAspect.SetAttribute(MusicAspect.ATTR_NUMTRACKS, (int) tag.Tag.TrackCount);
        try
        {
          mediaAspect.SetAttribute(MediaAspect.ATTR_DATE, new DateTime((int) tag.Tag.Year, 1, 1));
        }
        catch (ArgumentOutOfRangeException) { }

        if (tag.MimeType == "taglib/mp3")
        {
          // Handle the Rating, which comes from the POPM frame
          Tag id32_tag = tag.GetTag(TagTypes.Id3v2) as Tag;
          if (id32_tag != null)
          {
            PopularimeterFrame popm;
            foreach (Frame frame in id32_tag)
            {
              if (!(frame is PopularimeterFrame))
                continue;
              popm = (PopularimeterFrame) frame;
              mediaAspect.SetAttribute(MediaAspect.ATTR_RATING, (int) popm.Rating);
            }
          }
        }
      }
      catch (UnsupportedFormatException)
      {
        ServiceScope.Get<ILogger>().Info("Music metadata extractor: Unsupported music file '{0}'", path);
        return false;
      }
      catch (Exception ex)
      {
        // Only log at the info level here - And simply return false. This makes the importer know that we
        // couldn't perform our task here
        ServiceScope.Get<ILogger>().Info("Music metadata extractor: Exception reading file '{0}'", ex, path);
        return false;
      }
      return true;
    }

    #endregion
  }
}