using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DistancePostion : MonoBehaviour
{
    public Transform footTransform; // 在 Inspector 里把 FootAnchor 拖进来

    private void Update()
    {
        if (footTransform == null)
        {
            return;
        }

        Shader.SetGlobalVector("_Position", footTransform.position);
    }
}
