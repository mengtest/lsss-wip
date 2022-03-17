﻿using System.Text;
using Latios;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Lsss.Tools
{
    public partial class ProfilingDisplayUpdateSystem : SubSystem
    {
        Texture2D     m_texture       = new Texture2D(256, 256, TextureFormat.RGBA32, false);
        StringBuilder m_stringBuilder = new StringBuilder();

        protected override void OnUpdate()
        {
            var profilingData = worldBlackboardEntity.GetCollectionComponent<ProfilingData>(false);
            Entities.ForEach((Entity entity, ProfilerPanel panel) =>
            {
                if (panel.panel == null)
                {
                    Debug.LogError("Exists with destroyed panel");
                    Debug.LogError(EntityManager.Exists(entity));
                    var types = EntityManager.GetComponentTypes(entity);
                    foreach (var t in types)
                    {
                        Debug.LogError(t.GetManagedType());
                    }
                    return;
                }

                bool toggle = Keyboard.current.pKey.wasPressedThisFrame;
                if (toggle)
                    panel.panel.SetActive(!panel.panel.activeSelf);
                if (panel.panel.activeSelf)
                {
                    if (panel.image.texture == null)
                    {
                        panel.image.texture = m_texture;
                    }

                    m_texture.LoadRawTextureData(profilingData.image);
                    m_texture.Apply();

                    m_stringBuilder.Clear();
                    for (int i = 0; i < profilingData.barValues.Length; i++)
                    {
                        m_stringBuilder.Append(profilingData.barValues[i]);
                        m_stringBuilder.Append('\n');
                        m_stringBuilder.Append('\n');
                    }
                    panel.labels.SetText(m_stringBuilder);
                }
            }).WithoutBurst().Run();
        }
    }
}

