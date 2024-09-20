using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

namespace Custom2DLight
{
    [Serializable]
    internal class PointLightSetting
    {
        public Vector2 lightPosition;
        public float lightIntensity;
        public Color lightColor;
    }
    public class GetLightValue : MonoBehaviour
    {
        
        [SerializeField] private PointLightSetting[] lightSettings;
        
        private ComputeBuffer _pointLightBuffer;
        private int _propertyID;
        public Material testM;
        private void Start()
        {
            _pointLightBuffer = new ComputeBuffer(lightSettings.Length, 3 * sizeof(float) + 4 * sizeof(float));
            _propertyID = Shader.PropertyToID("_PointLightsBuffer");
            UpdateBuffer();
        }

        private void Update()
        {
            UpdateBuffer();
        }
        
        private struct PointLightData
        {
            public Vector2 LightPosition;
            public float LightIntensity;
            public Color LightColor;
        }

        private void UpdateBuffer()
        {
            PointLightData[] pointLightData = new PointLightData[lightSettings.Length];
            for (int i = 0; i < lightSettings.Length; i++)
            {
                pointLightData[i].LightPosition = lightSettings[i].lightPosition;
                pointLightData[i].LightIntensity = lightSettings[i].lightIntensity;
                pointLightData[i].LightColor = lightSettings[i].lightColor;
            }
            _pointLightBuffer.SetData(pointLightData);
            Shader.SetGlobalBuffer(_propertyID, _pointLightBuffer);
        }
        
        private void OnDestroy()
        {
            // 销毁ComputeBuffer
            if (_pointLightBuffer != null)
            {
                _pointLightBuffer.Release();
            }
        }
    }
}
