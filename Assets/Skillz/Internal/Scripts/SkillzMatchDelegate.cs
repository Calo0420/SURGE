using System;
using SkillzSDK;

/// <summary>
/// You should implement this interface as part of one of your Game Objects.
/// The instance of this object should persist for the lifetime of your game, once you've launched Skillz.
/// </summary>
public interface SkillzMatchDelegate
{
    /// <summary>
    /// This method is called when a user starts a match from Skillz
    /// This method is required to impelement.
    /// </summary>
    void OnMatchWillBegin(Match matchInfo);

    /// <summary>
    /// This method is called when a user exits the Skillz experience (via Menu -> Exit)
    /// This method is optional to impelement. This method is usually used only if your game has its own Main Menu.
    /// </summary>
    void OnSkillzWillExit();

    /// <summary>
    /// This method is called when a user enters the Progression Room (via Menu -> Progression Room)
    /// This method is optional to implement. This method should only be implemented if your game has
    /// own player progression experience.
    /// </summary>
    void OnProgressionRoomEnter();

    /// <summary>
    /// This method is called when a new paying user enters their first cash match
    /// This method is optional to implement. 
    /// </summary>
    void OnNPUConversion();

    /// <summary>
    /// This method is called when your game receives a memory warning.
    /// This method is optional to implement, and is only implemented on the Web
    /// </summary>
    void OnReceivedMemoryWarning();

	/// <summary>
	/// This method is called when a user enters the Tutorial Screen
    /// This method is optional to implement. This method should only be implemented if your game has
    /// own game tutorial.
	/// </summary>
    void OnTutorialScreenEnter()
    {
        // Default implementation - goes back to skillz if there's no tutorial screen.
        SkillzCrossPlatform.ReturnToSkillz();
    }
}

