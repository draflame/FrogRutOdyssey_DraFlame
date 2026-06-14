using System.Collections;
using UnityEngine;

public class FrogController : MonoBehaviour
{
    private IGameController game;
    public Vector2Int currentTile { get; private set; }
    private bool isMoving;

    [Header("Jump Settings")]
    [SerializeField] private float jumpDuration = 1.2f;
    [SerializeField] private float jumpHeight = 0.8f;

    private Animator animator;

    // Helper để khôi phục tham chiếu IGameController nếu bị mất sau Hot Reload
    private IGameController GetGameController()
    {
        if (game == null)
        {
            game = Object.FindAnyObjectByType<GameController>() as IGameController;
            if (game == null)
            {
                game = Object.FindAnyObjectByType<RandomGameController>() as IGameController;
            }
        }
        return game;
    }

    // ================= INIT =================
    public void Initialize(IGameController controller, Vector2Int startTile)
    {
        game = controller;
        currentTile = startTile;

        var ctrl = GetGameController();
        if (ctrl != null)
        {
            transform.position = ctrl.GetWorldPosition(currentTile);
        }

        animator = GetComponent<Animator>();
    }

    // ================= INPUT =================
    public void TryMove(Vector2Int target)
    {
        if (isMoving) return;
        
        var ctrl = GetGameController();
        if (ctrl == null)
        {
            Debug.LogError("[FrogController] Game controller reference is missing and cannot be restored!");
            return;
        }

        if (!ctrl.CanMove(currentTile, target)) return;

        StartCoroutine(JumpRoutine(target));
    }

    // ================= JUMP =================
    private IEnumerator JumpRoutine(Vector2Int target)
    {
        var ctrl = GetGameController();
        if (ctrl == null) yield break;

        ctrl.RecordMove(currentTile, target);
        isMoving = true;

        // Kích hoạt animation nhảy
        if (animator) animator.SetBool("Jump", true);

        // Biến ô cũ thành trống
        ctrl.ClearTile(currentTile);
        ctrl.ConsumeTile(currentTile);

        Vector3 startPos = transform.position;
        Vector3 endPos = ctrl.GetWorldPosition(target);

        float time = 0f;

        while (time < 1f)
        {
            time += Time.deltaTime / jumpDuration;

            // Di chuyển ngang
            Vector3 pos = Vector3.Lerp(startPos, endPos, time);

            // Tạo đường nhảy parabol
            float height = 4f * jumpHeight * time * (1f - time);
            pos.y += height;

            transform.position = pos;
            yield return null;
        }

        // Đưa về vị trí chính xác
        transform.position = endPos;
        currentTile = target;

        if (animator) animator.SetBool("Jump", false);
        
        isMoving = false;

        // Gọi các sự kiện sau khi nhảy xong
        ctrl = GetGameController();
        if (ctrl != null)
        {
            ctrl.OnFrogMoved(currentTile);
            ctrl.HighlightValidMoves(currentTile);
        }
    }

    public void ForceMove(Vector2Int tile)
    {
        StopAllCoroutines();
        isMoving = false;

        currentTile = tile;

        var ctrl = GetGameController();
        if (ctrl != null)
        {
            transform.position = ctrl.GetWorldPosition(tile);
        }

        if (animator)
            animator.SetBool("Jump", false);
    }
}
