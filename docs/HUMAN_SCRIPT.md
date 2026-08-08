# FALSE POSITIVE: human performance script

This is the spoken script for the game, arranged in story order. It is based on `STORY_SCRIPT.md` and the dialogue already present in the Unity project. It fills the two dialogue gaps in the canon: Nick's line during the fire argument and the officer's possible follow-ups during the live interrogation.

Every spoken line has a stable ID in the form `[CHARACTER-NNN]`. Use that ID as the corresponding voice-clip filename so dialogue and audio remain directly traceable. Text in brackets outside a speaker label is performance or scene direction and is not spoken. David's lines are reference responses for the player, not prerecorded voice-over. During the live interrogation, Officer Spassky should use only the lines that fit what the player has said. Alternative accusation paths and endings are mutually exclusive.

## Character voices

### Race and accent reference

Use these details when selecting or generating character voices. The listed race is the casting reference, with Russian and Indian indicating the intended accent family. Keep every accent natural and easy to understand.

| Character | Race and accent reference |
|---|---|
| David | White |
| Officer Spassky | Russian, with a Russian accent |
| Nick | Russian, with a Russian accent |
| Aaron | White |
| Ivy | White |
| Priya | Indian, with an Indian accent |
| Radio announcer | White |

**DAVID:** The player character. Hungover, frightened, and ashamed. He did not kill Nick, but he has spent two years hiding Nick and Ivy's affair from Aaron. When he hesitates, he is choosing what to admit, not searching for a story.

**OFFICER SPASSKY:** Male. Quiet, terse, and unhurried. He rarely raises his voice. He lets silence do the work, asks one question at a time, and corrects unsupported details without claiming that he can detect guilt or dishonesty.

**NICK:** Warm and careless. He knows the conversation with David matters, but he keeps trying to put it off.

**AARON:** Controlled, practical, and almost too calm. He redirects people by giving them something physical to do.

**IVY:** Frightened and guarded. Her alibi for Aaron comes too quickly because she knows what she saw and what admitting it would expose.

**PRIYA:** Openly panicked. She asks the questions everyone else is trying not to answer.

**RADIO ANNOUNCER:** Neutral and impersonal, partly obscured by static.

## Scene 1: the interrogation room

*Black screen. Spassky's voice begins muffled and distant. Each call is clearer than the last.*

**[SPASSKY-001] OFFICER SPASSKY:** David.

**[SPASSKY-002] OFFICER SPASSKY:** David.

**[SPASSKY-003] OFFICER SPASSKY:** David!

*The player wakes. If David says nothing for fifteen seconds, use the next line.*

**[SPASSKY-004] OFFICER SPASSKY:** You with me?

*The player may say anything. The prompted reference line is below.*

**[DAVID-001] DAVID:** Who are you? Where am I?

*Spassky leans back and opens the folder.*

**[SPASSKY-005] OFFICER SPASSKY:** I'm Officer Spassky. Nick is dead, and right now you're one of the suspects. I've already spoken to the others. Take your time and tell me everything you remember from last night.

## Scene 2: the cabin at night

### The argument by the fire

*This exchange happens shortly before Nick goes outside. David has been drinking. He is angry because Nick still refuses to tell Aaron about the affair.*

**[DAVID-002] DAVID:** You have to tell him, Nick. Say it out loud before Aaron works it out for himself.

**[NICK-001] NICK:** Not tonight, David. I can't do this with you right now. I need some air.

*Nick leaves. David does not see him pass through the doorway.*

### The radio

*David tunes the radio. The static clears just long enough for the warning.*

**[RADIO-001] RADIO ANNOUNCER:** A snowstorm is moving through the area. Please stay indoors until conditions improve.

*A door latch clicks offscreen. David looks up, but the door is already swinging closed. Nobody is visible.*

### David calls into the storm

*David opens the unlocked door. The player must shout this or call for Nick in their own words.*

**[DAVID-003] DAVID:** Nick!

*There is no answer.*

## Scene 3: the first recall

*This interrogation is live. The dialogue below is the full truthful route through the seven required story topics. Spassky may change the order, skip a question the player has already answered, or use the optional follow-ups later in this section.*

**[SPASSKY-006] OFFICER SPASSKY:** So. What's the last thing you remember?

**[DAVID-004] DAVID:** The fire. Nick and I were drinking. I remember the argument, then bits and pieces. After I lay down, there's nothing until Priya screamed.

**[SPASSKY-007] OFFICER SPASSKY:** Who else was drinking with you?

**[DAVID-005] DAVID:** All five of us had been drinking. Aaron and Ivy went upstairs. Priya passed out early. Nick and I stayed by the fire.

**[SPASSKY-008] OFFICER SPASSKY:** How much did you have?

**[DAVID-006] DAVID:** Too much. Enough that I blacked out on the sofa.

**[SPASSKY-009] OFFICER SPASSKY:** Tell me about the argument with Nick. What was it really about?

**[DAVID-007] DAVID:** Nick and Ivy. They'd been seeing each other for two years. I knew, and I kept it from Aaron. I told Nick he had to tell him before Aaron figured it out himself.

**[SPASSKY-010] OFFICER SPASSKY:** You kept that from your best friend for two years?

**[DAVID-008] DAVID:** Yes.

**[SPASSKY-011] OFFICER SPASSKY:** What did Nick do after the argument?

**[DAVID-009] DAVID:** I thought he went outside to cool off.

**[SPASSKY-012] OFFICER SPASSKY:** Thought?

**[DAVID-010] DAVID:** I heard the door. By the time I looked up, it was already closing. I didn't see who went through it.

**[SPASSKY-013] OFFICER SPASSKY:** What did you do next?

**[DAVID-011] DAVID:** I went to the door, opened it, and called for Nick.

**[SPASSKY-014] OFFICER SPASSKY:** Did he answer?

**[DAVID-012] DAVID:** No. The wind swallowed my voice. I didn't hear anything back.

**[SPASSKY-015] OFFICER SPASSKY:** Was the door locked when you opened it?

**[DAVID-013] DAVID:** No.

**[SPASSKY-016] OFFICER SPASSKY:** What did you do with it afterward?

**[DAVID-014] DAVID:** I left it unlocked. I thought Nick was around the side of the cabin and would come back in.

**[SPASSKY-017] OFFICER SPASSKY:** Then what?

**[DAVID-015] DAVID:** I went back to the sofa and passed out.

**[SPASSKY-018] OFFICER SPASSKY:** How long were you out?

**[DAVID-016] DAVID:** I don't know. I woke up when Priya screamed the next morning.

### Time follow-up

*Use one David response. The first applies only if the player inspected the mantel clock.*

**[SPASSKY-019] OFFICER SPASSKY:** What time did Nick leave?

**[DAVID-017] DAVID, IF HE SAW THE CLOCK:** The clock said twelve fifty-two when I looked at it. I went to the door a few minutes later.

**[DAVID-018] DAVID, IF HE DID NOT SEE THE CLOCK:** I don't know. It was after midnight, but I didn't check the clock.

### The morning account

**[SPASSKY-020] OFFICER SPASSKY:** Walk me through the morning. Start with Priya's scream.

**[DAVID-019] DAVID:** She was at the window. Nick was outside, face down in the snow. The pane was broken, but the grille was still intact, and the glass was on the cabin floor.

**[SPASSKY-021] OFFICER SPASSKY:** What happened at the door?

**[DAVID-020] DAVID:** It was locked. The key was hanging on the hook inside. I took it and unlocked the door.

**[SPASSKY-022] OFFICER SPASSKY:** Then?

**[DAVID-021] DAVID:** We ran outside. Aaron said Nick was cold and told us to bring him in by the fire. I took Nick's shoulders. Aaron took his legs. We carried him to the sofa.

**[SPASSKY-023] OFFICER SPASSKY:** You moved the body.

**[DAVID-022] DAVID:** Yes. I didn't stop to think about what we were doing.

**[SPASSKY-024] OFFICER SPASSKY:** What happened to Nick?

**[DAVID-023] DAVID:** I don't know everything that happened. I know he went out in a thin jacket, the door was locked after I passed out, and he died outside in the cold.

### Optional trap follow-ups

*These lines test whether David claims knowledge he could not have had. They can appear anywhere in the first recall after the related topic comes up.*

#### The closing door

**[SPASSKY-025] OFFICER SPASSKY:** You said you looked up. Who was it?

**[DAVID-024] DAVID, SUPPORTED RESPONSE:** I don't know. The door was already closing. I couldn't see anyone.

**[SPASSKY-026] OFFICER SPASSKY, IF DAVID NAMES SOMEONE:** You couldn't see the doorway clearly. Don't give me a face you never saw.

#### The exact time

**[SPASSKY-027] OFFICER SPASSKY:** You're very precise about one o'clock. How do you know?

**[DAVID-025] DAVID, IF HE SAW THE CLOCK:** I looked at the mantel clock. It said twelve fifty-two.

**[DAVID-026] DAVID, IF HE DID NOT SEE THE CLOCK:** I don't. I'm estimating.

**[SPASSKY-028] OFFICER SPASSKY, IF THE TIME IS UNSUPPORTED:** If you didn't look at the clock, don't give me a time.

#### A reply from Nick

**[SPASSKY-029] OFFICER SPASSKY:** Did you hear him out there, or did you assume?

**[DAVID-027] DAVID, SUPPORTED RESPONSE:** I assumed. I called his name and got nothing back.

**[SPASSKY-030] OFFICER SPASSKY, IF DAVID CLAIMS NICK ANSWERED:** You told me the storm swallowed your voice. Be precise about what you heard back.

#### Nick's location

**[SPASSKY-031] OFFICER SPASSKY:** How do you know he was even outside yet?

**[DAVID-028] DAVID, SUPPORTED RESPONSE:** I don't. I heard the door and assumed it was him.

**[SPASSKY-032] OFFICER SPASSKY, IF DAVID CLAIMS HE SAW NICK OUTSIDE:** You never saw Nick outside that night. Tell me what you know, not what fits afterward.

#### The lock

**[SPASSKY-033] OFFICER SPASSKY:** So the door was locked before you fell asleep, or after?

**[DAVID-029] DAVID, SUPPORTED RESPONSE:** After. It was unlocked when I left it, and locked when we found Nick in the morning. I was unconscious in between.

**[SPASSKY-034] OFFICER SPASSKY, IF DAVID CLAIMS TO KNOW WHO LOCKED IT:** You were unconscious when someone turned that key. You can draw a conclusion, but you cannot say you saw it happen.

#### The broken window

**[SPASSKY-035] OFFICER SPASSKY:** Did you hear glass?

**[DAVID-030] DAVID, SUPPORTED RESPONSE:** No. I was passed out.

**[SPASSKY-036] OFFICER SPASSKY, IF DAVID CLAIMS HE HEARD IT:** You said you were unconscious. How would you know what the window sounded like?

## Scene 4: the cabin in the morning

*Priya pulls back the curtains and sees Nick outside. Her first words should overlap and tumble out.*

**[PRIYA-001] PRIYA:** Guys! Help! Something's happened to Nick! Ivy! Aaron! David! Please, come here!

*Ivy and Aaron come downstairs. David finds the front door locked, takes the key from the hook inside, and unlocks it. Everyone runs out to Nick.*

**[PRIYA-002] PRIYA:** What do we do? What do we do?

**[IVY-001] IVY:** Oh my God. What happened to him? What do we do now?

**[AARON-001] AARON:** He's freezing. Let's get him inside, onto the sofa by the fire.

*David takes Nick's shoulders. Aaron takes his legs. They begin carrying him toward the cabin.*

**[PRIYA-003] PRIYA:** How did this happen?

**[IVY-002] IVY:** I don't know. I was upstairs with Aaron.

**[PRIYA-004] PRIYA:** All night?

*Ivy waits a fraction too long.*

**[IVY-003] IVY:** Yes. All night.

**[AARON-002] AARON:** Priya. Not now.

**[PRIYA-005] PRIYA:** The door was locked. Who locked it?

**[AARON-003] AARON:** Lift on three.

*They reach the sofa and lower Nick onto it.*

**[IVY-004] IVY:** Careful. Careful. Easy.

**[PRIYA-006] PRIYA:** Nick? Nick, can you hear me?

*Priya calls the police.*

**[PRIYA-007] PRIYA:** Police? Our friend is hurt. We found him outside in the snow. Please send someone. Please hurry.

## Scene 5: the verdict

*This phase is live. Begin with the exact opening line below.*

**[SPASSKY-037] OFFICER SPASSKY:** Tell me why I should spare your life.

### Truthful defense route

**[DAVID-031] DAVID:** Because I didn't kill Nick. I argued with him, let him walk out into that storm, and passed out. I kept the affair from Aaron. I helped move Nick's body. Those are the things I did. I did not lock that door.

**[SPASSKY-038] OFFICER SPASSKY:** You were wearing Nick's coat. Your hands were on the body, the door, and the key. You knew what Nick had done, and you kept it quiet. Why should I trust the part that clears you?

**[DAVID-032] DAVID:** Don't trust that part by itself. Look at the cabin. I left the door unlocked. It was locked from inside in the morning, and the key was still inside. The grille was intact and the glass was on the floor. Nobody came through that window.

**[SPASSKY-039] OFFICER SPASSKY:** If it's not you, then tell me who did it.

### Accusation route: Aaron

**[DAVID-033] DAVID:** Aaron.

**[SPASSKY-040] OFFICER SPASSKY:** So you think it's Aaron, huh? Tell me why.

**[DAVID-034] DAVID:** He found out about Nick and Ivy that night. Someone inside locked the door after I passed out. The key was inside, the grille was intact, and the glass was on the cabin floor. Ivy gave Aaron an alibi before anyone accused him. Then Aaron was the one who pushed us to move Nick.

**[SPASSKY-041] OFFICER SPASSKY:** Did you see Aaron lock the door?

**[DAVID-035] DAVID:** No. I didn't see it happen. I'm telling you where the evidence leads.

**[SPASSKY-042] OFFICER SPASSKY:** That's enough from you. I've heard enough.

### Accusation route: Ivy

**[DAVID-036] DAVID:** Ivy.

**[SPASSKY-043] OFFICER SPASSKY:** So you think it's Ivy, huh? Tell me why.

**[DAVID-037] DAVID:** She knew about Nick, and she was awake upstairs. She rushed to give Aaron an alibi before anyone asked for one. I think she knows what happened.

**[SPASSKY-044] OFFICER SPASSKY:** Knowing something happened is not the same as causing it. What puts her at the door?

**[DAVID-038] DAVID:** Nothing I saw. I can't put her there.

**[SPASSKY-045] OFFICER SPASSKY:** That's enough from you. I've heard enough.

### Accusation route: Priya

**[DAVID-039] DAVID:** Priya.

**[SPASSKY-046] OFFICER SPASSKY:** So you think it's Priya, huh? Tell me why.

**[DAVID-040] DAVID:** She found Nick and called the police. I don't have anything stronger than that.

**[SPASSKY-047] OFFICER SPASSKY:** She was asleep in the armchair, and she's the one who called us. Is that all you have?

**[DAVID-041] DAVID:** Yes.

**[SPASSKY-048] OFFICER SPASSKY:** That's enough from you. I've heard enough.

### No accusation or an ambiguous accusation

*Use these in order only if David refuses to name one person.*

**[SPASSKY-049] OFFICER SPASSKY:** That's not an answer. Try again.

**[SPASSKY-050] OFFICER SPASSKY:** You understand how that sounds, don't you?

**[SPASSKY-051] OFFICER SPASSKY:** Last chance. Who do you think did this?

*If David still gives no single name, Spassky ends the interrogation.*

**[SPASSKY-052] OFFICER SPASSKY:** That's enough from you. I've heard enough.

## Scene 6: endings

*Only one ending plays. The lines below are listed in ending order, not as a single conversation.*

### Ending A: David is taken

*Spassky closes the folder. Two officers wait at the door. Spassky does not look at David.*

**[SPASSKY-053] OFFICER SPASSKY:** You were the only one who couldn't tell me where you were.

### Ending B: Aaron is taken

*Aaron sits in the next interrogation room. He does not react as an officer reads to him.*

**[SPASSKY-054] OFFICER SPASSKY:** He locked it. You unlocked it. Only one of those was a decision.

### Ending C: Ivy is taken

*Ivy sits behind the observation glass and says nothing.*

**[SPASSKY-055] OFFICER SPASSKY:** She agreed with you. That's not the same as it being true.

### Ending D: Priya is taken

*Priya sits behind the observation glass, crying and asking what happened.*

**[PRIYA-008] PRIYA:** What happened? Why won't anyone tell me what happened?

**[SPASSKY-056] OFFICER SPASSKY:** She's the one who called us. Sit with that.

## Final card

*The following text appears on screen and is not spoken.*

> 14:20. Ivy has asked to make a second statement.
>
> She is still waiting.
>
> FALSE POSITIVE
