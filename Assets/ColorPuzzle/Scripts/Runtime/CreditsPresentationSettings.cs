using UnityEngine;

namespace ColorPuzzle
{
    /// <summary>Single shared source for the menu and final-game credits presentation.</summary>
    [CreateAssetMenu(fileName = "CreditsPresentationSettings", menuName = "Color Puzzle/Credits Presentation")]
    public sealed class CreditsPresentationSettings : ScriptableObject
    {
        [Header("Shared credits content")]
        [SerializeField] private string heading = "COLOR THE GREY";
        [SerializeField, TextArea(8, 20)] private string creditsText =
            "DEVELOPERS\nCassie Chu\nEvelyn Li\n\nAUDIO DESIGNERS\nDee Pera\nYiran Wang\nEleanor Xu\n\nCOURSE INSTRUCTORS\nJung-ho Sohn\nLivia Tseng";

        [Header("Shared rolling presentation")]
        [SerializeField, Min(1f)] private float rollDuration = 22f;
        [SerializeField, Min(0f)] private float holdDuration = 6f;

        public string Heading => heading;
        public string CreditsText => creditsText;
        public float RollDuration => rollDuration;
        public float HoldDuration => holdDuration;
    }
}
