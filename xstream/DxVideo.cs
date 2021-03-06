﻿using SharpDX;
using SharpDX.Direct3D9;
using System;
using System.Windows.Forms;

namespace Xstream
{
    unsafe class DxVideo
    {
        const int D3DADAPTER_DEFAULT = 0;// Used to specify the primary display adapter.

        Direct3D _d3d;
        Device _d3dDevice;

        Rectangle _rectOrigin;
        string _fontSourceRegular;
        string _fontSourceBold;

        public DxVideo(int width, int height, Form f)
        {
            _d3d = new Direct3D();

            PresentParameters pp = new PresentParameters();
            pp.DeviceWindowHandle = f.Handle;
            pp.BackBufferWidth = width;
            pp.BackBufferHeight = height;
            pp.BackBufferCount = 1;// 后备缓冲区的数量。通常设为“1”，即只有一个后备表面。
            /*
             * 指定系统如何将后台缓冲区的内容复制到前台缓冲区，从而在屏幕上显示。它的值有：
             * D3DSWAPEFFECT_DISCARD: 清除后台缓存的内容。
             * D3DSWAPEEFECT_FLIP: 保留后台缓存的内容，当缓存区>1时。
             * D3DSWAPEFFECT_COPY: 保留后台缓存的内容，缓冲区=1时。
             * 一般情况下使用D3DSWAPEFFECT_DISCARD
             */
            pp.SwapEffect = SwapEffect.Discard;
            pp.Windowed = true;// 指定窗口模式。True = 窗口模式；False = 全屏模式
            /*
             * 显示适配器刷新屏幕的速率。该值取决于应用程序运行的模式：
             * 对于窗口模式，刷新率必须为0。
             * 对于全屏模式，刷新率是EnumAdapterModes返回的刷新率之一。
             */
            pp.FullScreenRefreshRateInHz = 0;
            /*
             * 交换链的后缓冲区可以提供给前缓冲区的最大速率。可以用以下方式：
             * D3DPRESENT_INTERVAL_DEFAULT: 这几乎等同于D3DPRESENT_INTERVAL_ONE。
             * D3DPRESENT_INTERVAL_ONE: 垂直同步。当前的操作不会比刷新屏幕更频繁地受到影响。
             * D3DPRESENT_INTERVAL_IMMEDIATE: 以实时的方式来显示渲染画面。
             */
            pp.PresentationInterval = PresentInterval.One;

            CreateFlags device_flags = CreateFlags.FpuPreserve;
            Capabilities caps = _d3d.GetDeviceCaps(D3DADAPTER_DEFAULT, DeviceType.Hardware);
            if ((caps.DeviceCaps & DeviceCaps.HWTransformAndLight) != 0)
            {
                device_flags |= CreateFlags.HardwareVertexProcessing;
            }
            else
            {
                device_flags |= CreateFlags.SoftwareVertexProcessing;
            }
            //device_flags |= CreateFlags.Multithreaded;

            /*
             * @param adapter       表示显示适配器的序号。D3DADAPTER_DEFAULT(0)始终是主要的显示适配器。
             * @param renderWindow  窗体或任何其他Control派生类的句柄。此参数指示要绑定到设备的表面。
             *                      指定的窗口必须是顶级窗口。不支持空值。
             * @param deviceType    定义设备类型。
             *                      D3DDEVTYPE_HAL 硬件栅格化。可以使用软件，硬件或混合的变换和照明进行着色。
             *                      详见：https://docs.microsoft.com/en-us/windows/win32/direct3d9/d3ddevtype
             * @param behaviorFlags 控制设备创建行为的一个或多个标志的组合。
             *                      D3DCREATE_HARDWARE_VERTEXPROCESSING 指定硬件顶点处理。
             *                      D3DCREATE_SOFTWARE_VERTEXPROCESSING 指定软件顶点处理。
             *                      对于Windows 10版本1607及更高版本，不建议使用此设置。
             *                      使用D3DCREATE_HARDWARE_VERTEXPROCESSING。
             *                      [!Note] 除非没有可用的硬件顶点处理，
             *                      否则在Windows 10版本1607（及更高版本）中不建议使用软件顶点处理，
             *                      因为在提高实现安全性的同时，软件顶点处理的效率已大大降低。
             *                      详见：https://docs.microsoft.com/en-us/windows/win32/direct3d9/d3dcreate
             *
             * @see https://docs.microsoft.com/en-us/windows/win32/api/d3d9/nf-d3d9-idirect3d9-createdevice
             */
            _d3dDevice = new Device(_d3d
                , D3DADAPTER_DEFAULT
                , DeviceType.Hardware
                , f.Handle
                , device_flags
                , pp);

            _rectOrigin = new Rectangle(0, 0, width, height);
            _fontSourceRegular = $"{AppDomain.CurrentDomain.BaseDirectory}Fonts/Xolonium-Regular.ttf";
            _fontSourceBold = $"{AppDomain.CurrentDomain.BaseDirectory}Fonts/Xolonium-Bold.ttf";
        }

        public void Initialize()
        {
            _d3dDevice.VertexShader = null;
        }

        public void Close()
        {
        }
    }
}
