using UnityEngine;

public class EnemyAnimator2D : MonoBehaviour
{
    private static readonly int SpeedHash = Animator.StringToHash("Speed");
    private static readonly int AttackHash = Animator.StringToHash("Attack");
    private static readonly int HurtHash = Animator.StringToHash("Hurt");
    private static readonly int IsDeadHash = Animator.StringToHash("IsDead");

    [Header("References")]
    [SerializeField] private Animator animator;
    [SerializeField] private SpriteRenderer bodySpriteRenderer;

    public void SetSpeed(float speed)
    {
        if (animator != null)
        {
            animator.SetFloat(SpeedHash, Mathf.Abs(speed));
        }
    }

    public void TriggerAttack()
    {
        if (animator != null)
        {
            animator.SetTrigger(AttackHash);
        }
    }

    public void TriggerHurt()
    {
        if (animator != null)
        {
            animator.SetTrigger(HurtHash);
        }
    }

    public void SetDead(bool isDead)
    {
        if (animator != null)
        {
            animator.SetBool(IsDeadHash, isDead);
        }
    }

    public void FaceDirection(int direction)
    {
        if (bodySpriteRenderer == null || direction == 0)
        {
            return;
        }

        bodySpriteRenderer.flipX = direction < 0;
    }
}
