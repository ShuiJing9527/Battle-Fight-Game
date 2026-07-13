using System.Collections;
using UnityEngine;

public class AutoDestroyParticle : MonoBehaviour
{
    [SerializeField, Min(0f)] private float fallbackDestroyDelay = 1f;

    private Coroutine destroyRoutine;

    private void OnEnable()
    {
        if (destroyRoutine != null)
        {
            StopCoroutine(destroyRoutine);
        }

        destroyRoutine = StartCoroutine(DestroyWhenFinished());
    }

    private IEnumerator DestroyWhenFinished()
    {
        yield return null;

        ParticleSystem[] particleSystems = GetComponentsInChildren<ParticleSystem>(true);
        if (particleSystems == null || particleSystems.Length == 0)
        {
            yield return new WaitForSeconds(Mathf.Max(0f, fallbackDestroyDelay));
            Destroy(gameObject);
            yield break;
        }

        while (true)
        {
            bool anyAlive = false;
            for (int i = 0; i < particleSystems.Length; i++)
            {
                ParticleSystem particleSystem = particleSystems[i];
                if (particleSystem != null && particleSystem.IsAlive(true))
                {
                    anyAlive = true;
                    break;
                }
            }

            if (!anyAlive)
            {
                break;
            }

            yield return null;
        }

        Destroy(gameObject);
    }
}
