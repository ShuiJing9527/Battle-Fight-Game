using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TimedEnemyMoveSpeedDebuff : MonoBehaviour
{
    private readonly Dictionary<string, Coroutine> removalRoutines = new Dictionary<string, Coroutine>();

    public void ApplyOrRefresh(string key, float multiplier, float duration)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return;
        }

        EnemyDebuffReceiver receiver = GetComponent<EnemyDebuffReceiver>();
        if (receiver == null)
        {
            receiver = gameObject.AddComponent<EnemyDebuffReceiver>();
        }

        receiver.ApplyMoveSpeedMultiplier(key, Mathf.Max(0f, multiplier));

        if (removalRoutines.TryGetValue(key, out Coroutine existingRoutine) && existingRoutine != null)
        {
            StopCoroutine(existingRoutine);
        }

        removalRoutines[key] = StartCoroutine(RemoveAfterDuration(key, Mathf.Max(0.01f, duration)));
    }

    public static void ApplyOrRefresh(GameObject target, string key, float multiplier, float duration)
    {
        if (target == null)
        {
            return;
        }

        GameObject resolvedTarget = target;
        EnemyController enemyController = target.GetComponent<EnemyController>();
        if (enemyController == null)
        {
            enemyController = target.GetComponentInParent<EnemyController>(true);
        }
        if (enemyController == null)
        {
            enemyController = target.GetComponentInChildren<EnemyController>(true);
        }
        if (enemyController != null)
        {
            resolvedTarget = enemyController.gameObject;
        }

        TimedEnemyMoveSpeedDebuff debuff = resolvedTarget.GetComponent<TimedEnemyMoveSpeedDebuff>();
        if (debuff == null)
        {
            debuff = resolvedTarget.AddComponent<TimedEnemyMoveSpeedDebuff>();
        }

        debuff.ApplyOrRefresh(key, multiplier, duration);
    }

    private IEnumerator RemoveAfterDuration(string key, float duration)
    {
        yield return new WaitForSeconds(duration);

        EnemyDebuffReceiver receiver = GetComponent<EnemyDebuffReceiver>();
        receiver?.RemoveMoveSpeedMultiplier(key);
        removalRoutines.Remove(key);
    }

    private void OnDisable()
    {
        EnemyDebuffReceiver receiver = GetComponent<EnemyDebuffReceiver>();
        if (receiver != null)
        {
            foreach (string key in removalRoutines.Keys)
            {
                receiver.RemoveMoveSpeedMultiplier(key);
            }
        }

        removalRoutines.Clear();
    }
}
