/***************************************************************************
    copyright            : (C) 2005 by Brian Nickel
    email                : brian.nickel@gmail.com
    based on             : oggfile.cpp from TagLib
 ***************************************************************************/

/***************************************************************************
 *   This library is free software; you can redistribute it and/or modify  *
 *   it  under the terms of the GNU Lesser General Public License version  *
 *   2.1 as published by the Free Software Foundation.                     *
 *                                                                         *
 *   This library is distributed in the hope that it will be useful, but   *
 *   WITHOUT ANY WARRANTY; without even the implied warranty of            *
 *   MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU     *
 *   Lesser General Public License for more details.                       *
 *                                                                         *
 *   You should have received a copy of the GNU Lesser General Public      *
 *   License along with this library; if not, write to the Free Software   *
 *   Foundation, Inc., 59 Temple Place, Suite 330, Boston, MA  02111-1307  *
 *   USA                                                                   *
 ***************************************************************************/

using System.Collections;
using System.Collections.Generic;
using System;

namespace TagLib.Ogg
{
   [SupportedMimeType("taglib/ogg", "ogg")]
   [SupportedMimeType("application/ogg")]
   [SupportedMimeType("application/x-ogg")]
   [SupportedMimeType("audio/vorbis")]
   [SupportedMimeType("audio/x-vorbis")]
   [SupportedMimeType("audio/x-vorbis+ogg")]
   [SupportedMimeType("audio/ogg")]
   [SupportedMimeType("audio/x-ogg")]
   public class File : TagLib.File
   {
      //////////////////////////////////////////////////////////////////////////
      // private properties
      //////////////////////////////////////////////////////////////////////////
      private GroupedComment tag;
      private Properties properties;
      
      public File (string file, ReadStyle properties_style) : base (file)
      {
         Mode = AccessMode.Read;
         tag = new GroupedComment ();
         properties = null;
         Read (properties_style);
         Mode = AccessMode.Closed;
      }
      
      // Added by Albert/MediaPortal
      public File (IFileAbstraction fileAbstraction, ReadStyle properties_style) : base (fileAbstraction)
      {
         Mode = AccessMode.Read;
         tag = new GroupedComment ();
         properties = null;
         Read (properties_style);
         Mode = AccessMode.Closed;
      }
      
      private void Read (ReadStyle properties_style)
      {
         long end;
         Dictionary<uint, Bitstream> streams = ReadStreams (null, out end);
         
         foreach (uint id in streams.Keys)
            tag.AddComment (id, streams [id].Codec.CommentData);
         
         if (properties_style == ReadStyle.None)
            return;
         
         PageHeader last_header = LastPageHeader;
         properties = new Properties (streams, last_header, properties_style);
      }
      
      public override Tag Tag {get {return tag;}}
      
      public override TagLib.Properties Properties {get {return properties;}}
      public override TagLib.Tag GetTag (TagLib.TagTypes type, bool create)
      {
         if (type == TagLib.TagTypes.Xiph)
            return tag.Comments [0];
         return null;
      }
      public override void RemoveTags (TagLib.TagTypes types)
      {
         if ((types & TagLib.TagTypes.Xiph) != TagLib.TagTypes.NoTags)
            tag.Clear ();
      }
      
      public override void Save ()
      {
         Mode = AccessMode.Read;
         
         long end;
         List<Page> pages = new List<Page> ();
         Dictionary<uint, Bitstream> streams = ReadStreams (pages, out end);
         Dictionary<uint, Paginator> paginators = new Dictionary<uint, Paginator> ();
         List<List<Page>> new_pages = new List<List<Page>> ();
         
         foreach (Page page in pages)
         {
            uint id = page.Header.StreamSerialNumber;
            if (!paginators.ContainsKey (id))
               paginators.Add (id, new Paginator (streams [id].Codec));
            
            paginators [id].AddPage (page);
         }
         
         foreach (uint id in paginators.Keys)
         {
            paginators [id].SetComment (tag.GetComment (id));
            new_pages.Add (new List<Page> (paginators [id].Paginate ()));
         }
         
         ByteVector output = new ByteVector ();
         bool empty;
         do
         {
            empty = true;
            foreach (List<Page> stream_pages in new_pages)
            {
               if (stream_pages.Count == 0)
                  continue;
               
               output.Add (stream_pages [0].Render ());
               stream_pages.RemoveAt (0);
               
               if (stream_pages.Count != 0)
                  empty = false;
            }
         } while (!empty);
         
         Insert (output, 0, end);
         
         Mode = AccessMode.Closed;
      }
      
      private Dictionary<uint, Bitstream> ReadStreams (List<Page> pages, out long end)
      {
         Dictionary<uint, Bitstream> streams = new Dictionary<uint, Bitstream> ();
         List<Bitstream> active_streams = new List<Bitstream> ();
         
         long position = 0;
         
         do
         {
            Bitstream stream = null;
            Page page = new Page (this, position);
            
            if (page.Header.FirstPageOfStream)
            {
               stream = new Bitstream (page);
               streams.Add (page.Header.StreamSerialNumber, stream);
               active_streams.Add (stream);
            }
            
            if (stream == null)
               stream = streams [page.Header.StreamSerialNumber];
            
            if (active_streams.Contains (stream) && stream.ReadPage (page))
               active_streams.Remove (stream);
            
            if (pages != null)
               pages.Add (page);
            
            position += page.Size;
         }
         while (active_streams.Count > 0);
         
         end = position;
         
         return streams;
      }
      
      private PageHeader LastPageHeader
      {
         get
         {
            long last_page_header_offset = RFind ("OggS");

            if(last_page_header_offset < 0)
               throw new CorruptFileException ("Could not find last header.");

            return new PageHeader (this, last_page_header_offset);
         }
      }
   }
}