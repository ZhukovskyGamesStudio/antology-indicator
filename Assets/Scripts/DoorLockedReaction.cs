using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>
/// Реакция на клик по входной двери, пока выход ещё не открыт (до финала).
/// Как только <see cref="StoryManager.CanExit"/> становится true — молчит,
/// уступая настоящему выходу (ApartmentsExit).
///
/// Вешается на тот же объект, что и InteractiveObj двери. Клик ловим так же,
/// как <see cref="InteractiveObj"/> — по OnMouseDown + рейкасту в пределах дистанции.
/// </summary>
public class DoorLockedReaction : MonoBehaviour {
    [Tooltip("Реплика при попытке уйти до финала")]
    [TextArea] public string reaction = "Не помню, куда дел ключи...";

    private void OnMouseDown() {
        // Держим предмет — с миром не взаимодействуем (как в InteractiveObj).
        if (FirstPersonController.isHolding) {
            return;
        }

        // Выход уже открыт (финал) — дверь работает сама, реакция не нужна.
        if (StoryManager.CanExit) {
            return;
        }

        if (CursorRaycast.Raycast(out RaycastHit hit) && hit.distance <= CursorRaycast.RangeStatic
            && TalkUI.instance != null) {
            TalkUI.instance.Say(reaction).Forget();
        }
    }
}
