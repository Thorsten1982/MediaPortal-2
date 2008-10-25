#pragma region Copyright (C) 2007-2008 Team MediaPortal

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

#pragma endregion

//-----------------------------------------------------------------------------------
// Based on code originally written by Rob Philpott and published on CodeProject.com:
//   http://www.codeproject.com/KB/audio-video/Asio_Net.aspx
//
// ASIO is a trademark and software of Steinberg Media Technologies GmbH
//-----------------------------------------------------------------------------------

#include "ASIOSampleType.h"
#include "ChannelBuffer.h"

#pragma once
#pragma managed

using namespace System;

namespace Media
{
  namespace Players
  {
    namespace BassPlayer
    {
      namespace ASIOInterop
      {
        // represents a single audio channel (input or output) on the soundcard
        public ref class Channel
        {
        internal:

          // true is this is an input channel
          bool _isInput;

          // the channel name
          String^ _name;

          // sample format
          AsioSampleType _sampleType;

          // the actual buffer
          ChannelBuffer^ _buffer;

          // clip value for our float sample data
          float maxSampleValue;

          // internal construction only
          Channel(IAsio* pAsio, bool IsInput, int channelNumber, void* pTheirBuffer0, void* pTheirBuffer1);

        public:

          // the channel name
          property String^ Name { String^ get(); }

          // the sample type
          property AsioSampleType SampleType { AsioSampleType get(); }

          // indexer for setting the value of sample in the buffer
          property float default[int] { void set(int sample, float value); float get(int sample); }

        };
      };
    }
  }
}