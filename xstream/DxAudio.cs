﻿namespace Xstream
{
    class DxAudio
    {
        int _sampleRate;
        int _channels;

        public DxAudio(int sampleRate, int channels)
        {
            _sampleRate = sampleRate;
            _channels = channels;
        }
    }
}
