using UnityEngine;

public class PlayerVisualsController : MonoBehaviour
{
    private Animator _animator;
    private Vector2 _lastMoveDirection = Vector2.right; // 停止時の向きを保持するため

    void Awake()
    {
        _animator = GetComponentInParent<Animator>();
    }

    /// <summary>
    /// MiningToolsControllerから呼び出され、Animatorに道具の種類を教える
    /// </summary>
    public void SetToolAnimationType(int toolId)
    {
        if (_animator != null)
        {
            // Blend TreeはFloatしか受け付けないため、intをfloatにキャストして設定する
            _animator.SetFloat("ToolType", (float)toolId);
        }
    }

    /// <summary>
    /// 外部（MiningToolBehaviour）から呼び出され、採掘アニメーションを開始する
    /// </summary>
    public void TriggerMineAnimation(string toolStateName, Vector3 direction)
    {
        if (_animator == null) return;

        // 向きを設定
        Vector2 animDirection = GetAnimationDirection(direction);
        _animator.SetFloat("DirectionX", animDirection.x);
        _animator.SetFloat("DirectionY", animDirection.y);
        
        // 掘削アニメーションを開始するフラグを立てる
        _animator.SetBool("MineWithPickaxe", true);
    }

    /// <summary>
    /// アニメーションイベントから呼び出され、採掘アニメーションを終了する
    /// </summary>
    public void OnMineAnimationEnd()
    {
        if (_animator == null) return;
        _animator.SetBool("MineWithPickaxe", false);
    }

    /// <summary>
    /// 8方向のベクトルを、アニメーション用の4方向（上下左右）のベクトルに変換する
    /// </summary>
    /// <param name="direction">入力方向ベクトル</param>
    /// <returns>アニメーション用の(x, y)ベクトル</returns>
    private Vector2 GetAnimationDirection(Vector3 direction)
    {
        float y = direction.y;
        float x = direction.x;

        // 非常に小さい入力は無視
        if (x * x + y * y < 0.1f)
        {
            // ゼロベクトルに近い場合は、デフォルトの向き（例：右）を返すか、ゼロを返す
            return new Vector2(1, 0); 
        }

        // 垂直方向の入力が水平方向より明らかに強い場合のみ上下と判定
        // これにより、斜め入力は左右に分類される
        if (Mathf.Abs(y) > Mathf.Abs(x) * 2) // yがxの2倍以上大きい場合
        {
            if (y > 0)
            {
                return Vector2.up; // 上
            }
            else
            {
                return Vector2.down; // 下
            }
        }
        else // それ以外はすべて左右で判定
        {
            if (x > 0)
            {
                return Vector2.right; // 右
            }
            else
            {
                return Vector2.left; // 左
            }
        }
    }

    /// <summary>
    /// 移動に応じてアニメーションを更新する
    /// </summary>
    public void UpdateMovementAnimation(Vector3 moveDirection)
    {
        if (_animator == null) return;

        bool isMoving = moveDirection.sqrMagnitude > 0.1f;
        _animator.SetBool("isMoving", isMoving);

        // 移動中のみ向きを更新する
        if (isMoving)
        {
            _lastMoveDirection = GetAnimationDirection(moveDirection);
        }

        // 常に最後の向き情報をAnimatorに渡す
        _animator.SetFloat("moveX", _lastMoveDirection.x);
        _animator.SetFloat("moveY", _lastMoveDirection.y);
    }
}
