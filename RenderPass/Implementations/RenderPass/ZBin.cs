﻿using System;
using System.Collections.Generic;
using UnityEngine;
using HypnosRenderPipeline.Tools;

namespace HypnosRenderPipeline.RenderPass
{
    public class ZBin : BaseRenderPass
    {
        [NodePin(PinType.In, true)]
        public LightListPin lights = new LightListPin();

        [NodePin(PinType.Out)]
        public BufferPin<LightStructGPU> lightBuffer = new BufferPin<LightStructGPU>(200);

        [NodePin(PinType.Out)]
        public BufferPin<uint> tileLights = new BufferPin<uint>(200);

        [NodePin(PinType.In)]
        public TexturePin depth = new TexturePin(new RenderTextureDescriptor(1, 1, RenderTextureFormat.Depth, 24), colorCastMode: ColorCastMode.Fixed);

        List<LightStructGPU> lightBufferCPU = new List<LightStructGPU>();

        static ComputeShaderWithName zbin = new ComputeShaderWithName("Shaders/Tools/ZBin");

        [Tooltip("xy: tile count of screen, z: depth split count")]
        public Vector3Int tileCount = new Vector3Int(128, 64, 24);
        static Vector3Int minTileCount = new Vector3Int(8, 8, 8);
        static Vector3Int maxTileCount = new Vector3Int(256, 128, 24);

        [Tooltip("max light number per tile")]
        [Range(16, 200)]
        public int maxLightCountPerTile = 64;

        public bool includeRTLight = false;

        public float faraway = 100;


        public override void Execute(RenderContext context)
        {
            tileCount = Vector3Int.Min(maxTileCount, Vector3Int.Max(minTileCount, tileCount));
            tileCount = tileCount / 8 * 8;

            var local_lights = lights.handle.locals;
            var cam = context.camera;
            var cb = context.commandBuffer;

            tileLights.ReSize(tileCount.x * tileCount.y * (maxLightCountPerTile + 1));

            lightBufferCPU.Clear();
            foreach (var light in local_lights)
            {
                if (includeRTLight || light.shadow != HRPLightShadowType.RayTrace)
                {
                    lightBufferCPU.Add(light.lightStructGPU);
                }
            }
            var lightCount = lightBufferCPU.Count;
            lightBuffer.ReSize(lightCount);
            cb.SetBufferData(lightBuffer, lightBufferCPU);

            cb.SetGlobalInt("_LocalLightCount", lightCount);
            cb.SetGlobalFloat("_Faraway", faraway + 10);
            cb.SetGlobalBuffer("_LocalLightBuffer", lightBuffer);

            cb.SetGlobalVector("_TileCount", new Vector4(tileCount.x, tileCount.y, tileCount.z, maxLightCountPerTile));

            cb.SetComputeBufferParam(zbin, 0, "_TileLights", tileLights);
            cb.DispatchCompute(zbin, 0, tileCount.x / 4, tileCount.y / 2, 1);

            cb.SetComputeBufferParam(zbin, 1, "_TileLights", tileLights);
            cb.DispatchCompute(zbin, 1, tileCount.x, tileCount.y, (tileCount.z + 31) / 32);

            cb.SetGlobalBuffer("_TileLights", tileLights);

            //cb.SetComputeBufferParam(zbin, 1, "_TileLights", tileLights);
            //cb.SetComputeTextureParam(zbin, 1, "_Depth", depth);
            //cb.SetComputeTextureParam(zbin, 1, "_Debug", outDebug);
            //cb.SetGlobalVector("_WH", new Vector4(cam.pixelWidth, cam.pixelHeight));
            //cb.DispatchCompute(zbin, 1, cam.pixelWidth / 8, cam.pixelHeight / 8, 1);

            //cb.Blit(outDebug, target);

            //cb.DispatchCompute(zbin, 0, lightCount / 32 + (lightCount % 32 != 0 ? 1 : 0), 1, 1);
        }
    }
}
