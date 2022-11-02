using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;


public class ToyRenderPipeline : RenderPipeline
{

    RenderTexture gdepth;                                              // depth attachment
    RenderTexture[] gbuffers = new RenderTexture[4];                   //四个颜色纹理
    RenderTargetIdentifier[] gbufferID = new RenderTargetIdentifier[4];//给四个颜色纹理赋四个ID

    public Cubemap diffuseIBL;
    public Cubemap specularIBL;
    public Texture brdfLut;

    public ToyRenderPipeline()
    {
        // 创建纹理作为RenderTarget
        gdepth      = new RenderTexture(Screen.width, Screen.height, 24, RenderTextureFormat.Depth, RenderTextureReadWrite.Linear);
        gbuffers[0] = new RenderTexture(Screen.width, Screen.height, 0,  RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
        gbuffers[1] = new RenderTexture(Screen.width, Screen.height, 0,  RenderTextureFormat.ARGB2101010, RenderTextureReadWrite.Linear);
        gbuffers[2] = new RenderTexture(Screen.width, Screen.height, 0,  RenderTextureFormat.ARGB64, RenderTextureReadWrite.Linear);
        gbuffers[3] = new RenderTexture(Screen.width, Screen.height, 0,  RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);

        // 绑定ID
        for (int i = 0; i < 4; i++)
            gbufferID[i] = gbuffers[i];
    }
    protected override void Render(ScriptableRenderContext context, Camera[] cameras)
    {
        //ScriptableRenderContext负责接受一系列图形命令,反正就是上下文,按照openGL去理解就行
        Camera camera = cameras[0];//配置主摄像机


        context.SetupCameraProperties(camera);//把主摄像机加到上下文当中。

        CommandBuffer cmd = new CommandBuffer();//这是一个缓冲,用来配置若干条图形命令
        cmd.name = "gbuffer";
        cmd.SetGlobalTexture("_gdepth", gdepth);
        for (int i = 0; i < 4; i++)
            cmd.SetGlobalTexture("_GT" + i, gbuffers[i]);

        // 设置相机矩阵
        Matrix4x4 viewMatrix = camera.worldToCameraMatrix;
        Matrix4x4 projMatrix = GL.GetGPUProjectionMatrix(camera.projectionMatrix, false);
        Matrix4x4 vpMatrix = projMatrix * viewMatrix;
        Matrix4x4 vpMatrixInv = vpMatrix.inverse;
        cmd.SetGlobalMatrix("_vpMatrix", vpMatrix);
        cmd.SetGlobalMatrix("_vpMatrixInv", vpMatrixInv);
        
        //设置IBL贴图
        cmd.SetGlobalTexture("_diffuseIBL",diffuseIBL);
        cmd.SetGlobalTexture("_specularIBL",specularIBL);
        cmd.SetGlobalTexture("_brdfLut",brdfLut);


        // ����
        cmd.SetRenderTarget(gbufferID, gdepth);//先绑定RT
        cmd.ClearRenderTarget(true, true, Color.clear);//再清空初始化RT
        context.ExecuteCommandBuffer(cmd);//相当于配置好若干条命令之后,使用这条语句打包把所有命令加入到上下文当中

        //剔除
        camera.TryGetCullingParameters(out var cullingParameters);
        var cullingResults = context.Cull(ref cullingParameters);

        // 这里的逻辑很简单,主要就是一个shader一个排序问题,把对应的对象创建好之后加到设置里头
        ShaderTagId shaderTagId = new ShaderTagId("gbuffer");   // ʹ�� LightMode Ϊ gbuffer �� shader
        SortingSettings sortingSettings = new SortingSettings(camera);
        DrawingSettings drawingSettings = new DrawingSettings(shaderTagId, sortingSettings);
        FilteringSettings filteringSettings = FilteringSettings.defaultValue;
        



        context.DrawRenderers(cullingResults, ref drawingSettings, ref filteringSettings);//把这些参数加入到上下文当中和上面Excute有点像
        LightPass(context, camera);
        // skybox and Gizmos
        context.DrawSkybox(camera);

        
        if (Handles.ShouldRenderGizmos())
        {
            context.DrawGizmos(camera, GizmoSubset.PreImageEffects);
            context.DrawGizmos(camera, GizmoSubset.PostImageEffects);
        }
      
        context.Submit();//先这样简单的理解,首先这个管线的Render函数有一个上下文负责接受一系列的图形命令,所谓的Render的功能也就是用很多代码在往上下文当中添加绘制指令,
        //不论是直接在上下文中加,还是创建一个CommandBuffer,先把指令加到CommandBuffer当中,再打包将这个指令buffer加入到上下文当中,在整个函数的最后,也就是所有指令添加完
        //成之后上下文会调用一个Submit函数对所有的指令进行提交
    }
    void LightPass(ScriptableRenderContext context, Camera camera)
    {
        // ʹ�� Blit
        CommandBuffer cmd = new CommandBuffer();
        cmd.name = "lightpass";

        Material mat = new Material(Shader.Find("ToyRP/lightpass"));//输入某个shader的名字,创建一种材质
        cmd.Blit(gbufferID[0], BuiltinRenderTextureType.CameraTarget, mat);//Bilt指令参数分别有一个输入纹理和输出纹理和计算材质,相当于用输入纹理通过材质计算得到结果写入到输出纹理
        //当然我们着色计算要非常多的RT不止输入的这一个,不过没关系,我们在前面已经把所有的RT都设置成全局纹理了,计算的时候都可以sampler2D进来
        context.ExecuteCommandBuffer(cmd);
    }

}
