using System.Collections;
using UnityEngine;

public class CharacterViewTestDriver : MonoBehaviour
{
    [SerializeField] private CharacterView characterView;
    [SerializeField] private DamageNumberManager damageNumberManager;
    [SerializeField] private Transform testHitPoint;

    private bool isPlaying;

    private void Update()
    {
        if (isPlaying)
            return;

        if (Input.GetKeyDown(KeyCode.Alpha1))
        {
            StartCoroutine(
                PlayTestAction(ActionType.NormalAttack, 10));
        }

        if (Input.GetKeyDown(KeyCode.Alpha2))
        {
            StartCoroutine(
                PlayTestAction(ActionType.Duel, 7));
        }

        if (Input.GetKeyDown(KeyCode.Alpha3))
        {
            StartCoroutine(
                PlayTestAction(ActionType.Preparation, 0));
        }

        if (Input.GetKeyDown(KeyCode.Alpha4))
        {
            StartCoroutine(
                PlayTestAction(ActionType.Prestige, 25));
        }

        if (Input.GetKeyDown(KeyCode.H))
        {
            characterView.PlayHit();
        }

        if (Input.GetKeyDown(KeyCode.K))
        {
            characterView.PlayDead();
        }

        if (Input.GetKeyDown(KeyCode.Z))
        {
            characterView.SetVisualStateForTest(
                CharacterVisualState.Normal);
        }

        if (Input.GetKeyDown(KeyCode.X))
        {
            characterView.SetVisualStateForTest(
                CharacterVisualState.Weakened);
        }

        if (Input.GetKeyDown(KeyCode.C))
        {
            characterView.SetVisualStateForTest(
                CharacterVisualState.Broken);
        }
    }

    private IEnumerator PlayTestAction(
        ActionType actionType,
        int damage)
    {
        isPlaying = true;

        yield return characterView.PlayAction(
            actionType,
            () =>
            {
                if (damageNumberManager == null)
                    return;

                if (testHitPoint == null)
                    return;

                if (damage <= 0)
                    return;

                damageNumberManager.ShowDamage(
                    testHitPoint.position,
                    damage);
            });

        isPlaying = false;
    }
}