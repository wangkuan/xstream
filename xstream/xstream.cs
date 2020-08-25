﻿using SmartGlass.Common;
using System;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;
using Xstream.Codec;

namespace Xstream
{
    public partial class Xstream : Form
    {
        readonly CancellationTokenSource _cancellationTokenSource;
        DxAudio _audioRenderer;
        DxVideo _videoRenderer;
        public DxInput Input;

        public FFmpegDecoder Decoder;

        public event EventHandler<InputEventArgs> HandleInputEvent;
        bool _useController;

        GamestreamConfiguration _config;

        public Xstream(bool useController, GamestreamConfiguration config)
        {
            _useController = useController;
            _config = config;

            ClientSize = new Size(_config.VideoMaximumWidth, _config.VideoMaximumHeight);

            KeyPreview = Program.GetSettingBool("useController.KeyPreview");
            KeyPress += (sender, e) =>
            {
                MessageBox.Show("Form.KeyPress: '" + e.KeyChar.ToString() + "' consumed.");
            };

            InitializeComponent();

            // DirectX / FFMPEG setup

            _cancellationTokenSource = new CancellationTokenSource();

            _audioRenderer = new DxAudio(
                (int)Program.AudioFormat.SampleRate, (int)Program.AudioFormat.Channels);
            _videoRenderer = new DxVideo(
                (int)Program.VideoFormat.Width, (int)Program.VideoFormat.Height, this);

            Decoder = new FFmpegDecoder(Program.Nano, Program.AudioFormat, Program.VideoFormat);

            if (_useController)
            {
                Input = new DxInput($"{AppDomain.CurrentDomain.BaseDirectory}/gamecontrollerdb.txt");
                HandleInputEvent += Input.HandleInput;
            }

            Program.Nano.AudioFrameAvailable += Decoder.ConsumeAudioData;
            Program.Nano.VideoFrameAvailable += Decoder.ConsumeVideoData;

            Shown += MainLoop;
        }

        public void MainLoop(object sender, EventArgs e)
        {
            if (_useController && !Input.Initialize(this))
                throw new InvalidOperationException("Failed to init DirectX Input");

        }
    }
}
