import { audioGain } from './audio-gains.js';
import { TRUTH_CINEMATICS, TRUTH_SEQUENCE } from './truth-reveal.js';
/** Canonical shipped Unity recipes, extracted from _Persistent.unity at baseline b26a3f7.
 * Dialogue, original audio IDs, flags, order and pacing are preserved.
 * Staging is performed by the XR scene; these are not video placeholders. */
export const CINEMATICS = Object.freeze({
  ...TRUTH_CINEMATICS,
  "Wake": {
    "id": "Wake",
    "canonId": "CS-01",
    "stage": "interrogation",
    "fadeOutSeconds": 0,
    "fadeInSeconds": 0,
    "keepScreenLit": true,
    "endsBlack": false,
    "beats": [
      {
        "speaker": "???",
        "text": "David.",
        "audio": "/media/vo/SPASSKY-001.wav",
        "duration": 2.15,
        "holdSeconds": 1.2,
        "stage": "interrogation",
        "timeCard": "",
        "timeCardCaption": "",
        "timeCardHoldSeconds": 3.5,
        "dipToBlackBefore": false,
        "memoryFlag": ""
      },
      {
        "speaker": "???",
        "text": "David.",
        "audio": "/media/vo/SPASSKY-002.wav",
        "duration": 3.35,
        "holdSeconds": 1,
        "stage": "interrogation",
        "timeCard": "",
        "timeCardCaption": "",
        "timeCardHoldSeconds": 3.5,
        "dipToBlackBefore": false,
        "memoryFlag": ""
      },
      {
        "speaker": "???",
        "text": "David!",
        "audio": "/media/vo/SPASSKY-003.wav",
        "duration": 1.59,
        "holdSeconds": 1,
        "stage": "interrogation",
        "timeCard": "",
        "timeCardCaption": "",
        "timeCardHoldSeconds": 3.5,
        "dipToBlackBefore": false,
        "memoryFlag": ""
      }
    ]
  },
  "SpasskyAnswer": {
    "id": "SpasskyAnswer",
    "canonId": "CS-02",
    "stage": "interrogation",
    "fadeOutSeconds": 0,
    "fadeInSeconds": 0,
    "keepScreenLit": true,
    "endsBlack": false,
    "beats": [
      {
        "speaker": "SPASSKY",
        "text": "I'm Officer Spassky. Nick is dead, and right now you're one of the suspects. I've already spoken to the others. Take your time and tell me everything you remember from last night.",
        "audio": "/media/vo/SPASSKY-005.wav",
        "duration": 10.55,
        "holdSeconds": 8,
        "stage": "interrogation",
        "timeCard": "",
        "timeCardCaption": "",
        "timeCardHoldSeconds": 3.5,
        "dipToBlackBefore": false,
        "memoryFlag": ""
      }
    ]
  },
  "NightArgument": {
    "id": "NightArgument",
    "canonId": "M1-ARGUMENT",
    "stage": "night",
    "fadeOutSeconds": 0,
    "fadeInSeconds": 0,
    "keepScreenLit": true,
    "endsBlack": false,
    "beats": [
      {
        "speaker": "DAVID",
        "text": "You have to tell him, Nick. Say it out loud before Aaron works it out for himself.",
        "audio": null,
        "duration": 4,
        "holdSeconds": 4,
        "stage": "night",
        "timeCard": "",
        "timeCardCaption": "",
        "timeCardHoldSeconds": 3.5,
        "dipToBlackBefore": false,
        "memoryFlag": ""
      },
      {
        "speaker": "NICK",
        "text": "Not tonight, David. I can't do this with you right now. I need some air.",
        "audio": "/media/vo/NICK-001.wav",
        "duration": 5.75,
        "holdSeconds": 4,
        "stage": "night",
        "timeCard": "",
        "timeCardCaption": "",
        "timeCardHoldSeconds": 3.5,
        "dipToBlackBefore": false,
        "memoryFlag": ""
      }
    ]
  },
  "FuzzyToNight": {
    "id": "FuzzyToNight",
    "canonId": "CS-03",
    "stage": "transition-night",
    "fadeOutSeconds": 1.2,
    "fadeInSeconds": 0,
    "keepScreenLit": false,
    "endsBlack": true,
    "beats": [
      {
        "speaker": "",
        "text": "",
        "audio": null,
        "duration": 1.4,
        "holdSeconds": 1.4,
        "stage": "transition-night",
        "timeCard": "",
        "timeCardCaption": "",
        "timeCardHoldSeconds": 3.5,
        "dipToBlackBefore": false,
        "memoryFlag": ""
      }
    ]
  },
  "StandFromChair": {
    "id": "StandFromChair",
    "canonId": "CS-04",
    "stage": "night",
    "fadeOutSeconds": 0,
    "fadeInSeconds": 0,
    "keepScreenLit": true,
    "endsBlack": false,
    "beats": [
      {
        "speaker": "",
        "text": "",
        "audio": "/media/sfx/chair_creak.mp3",
        "duration": 1.515,
        "holdSeconds": 0.6,
        "stage": "night",
        "timeCard": "",
        "timeCardCaption": "",
        "timeCardHoldSeconds": 3.5,
        "dipToBlackBefore": false,
        "memoryFlag": ""
      },
      {
        "speaker": "",
        "text": "",
        "audio": null,
        "duration": 2.95,
        "holdSeconds": 2.95,
        "stage": "night",
        "timeCard": "",
        "timeCardCaption": "",
        "timeCardHoldSeconds": 3.5,
        "dipToBlackBefore": false,
        "memoryFlag": ""
      }
    ]
  },
  "RadioClears": {
    "id": "RadioClears",
    "canonId": "CS-05",
    "stage": "night",
    "fadeOutSeconds": 0,
    "fadeInSeconds": 0,
    "keepScreenLit": true,
    "endsBlack": false,
    "beats": [
      {
        "speaker": "",
        "text": "",
        "audio": null,
        "duration": 3.4,
        "holdSeconds": 3.4,
        "stage": "night",
        "timeCard": "",
        "timeCardCaption": "",
        "timeCardHoldSeconds": 3.5,
        "dipToBlackBefore": false,
        "memoryFlag": ""
      },
      {
        "speaker": "RADIO",
        "text": "A snowstorm is moving through the area. Please stay indoors until conditions improve.",
        "audio": "/media/vo/RADIO-001.wav",
        "duration": 5.11,
        "holdSeconds": 3,
        "stage": "night",
        "timeCard": "",
        "timeCardCaption": "",
        "timeCardHoldSeconds": 3.5,
        "dipToBlackBefore": false,
        "memoryFlag": "heard_radio_warning"
      },
      {
        "speaker": "",
        "text": "",
        "audio": null,
        "duration": 0.3,
        "holdSeconds": 0.3,
        "stage": "night",
        "timeCard": "",
        "timeCardCaption": "",
        "timeCardHoldSeconds": 3.5,
        "dipToBlackBefore": false,
        "memoryFlag": ""
      }
    ]
  },
  "SomeoneLeft": {
    "id": "SomeoneLeft",
    "canonId": "CS-06",
    "stage": "night",
    "fadeOutSeconds": 0,
    "fadeInSeconds": 0,
    "keepScreenLit": true,
    "endsBlack": false,
    "beats": [
      {
        "speaker": "",
        "text": "",
        "audio": null,
        "duration": 1.6,
        "holdSeconds": 1.5,
        "stage": "night",
        "timeCard": "",
        "timeCardCaption": "",
        "timeCardHoldSeconds": 3.5,
        "dipToBlackBefore": false,
        "memoryFlag": "saw_door_close"
      }
    ]
  },
  "CallForNick": {
    "id": "CallForNick",
    "canonId": "CS-07",
    "stage": "night-door",
    "fadeOutSeconds": 0,
    "fadeInSeconds": 0,
    "keepScreenLit": true,
    "endsBlack": false,
    "beats": [
      {
        "speaker": "",
        "text": "",
        "audio": "/media/sfx/wind_gust_roar.mp3",
        "duration": 3.03,
        "holdSeconds": 1.8,
        "stage": "night-door",
        "timeCard": "",
        "timeCardCaption": "",
        "timeCardHoldSeconds": 3.5,
        "dipToBlackBefore": false,
        "memoryFlag": ""
      }
    ]
  },
  "FuzzyToInterrogation": {
    "id": "FuzzyToInterrogation",
    "canonId": "CS-08",
    "stage": "transition-interrogation",
    "fadeOutSeconds": 1.2,
    "fadeInSeconds": 0,
    "keepScreenLit": false,
    "endsBlack": true,
    "beats": [
      {
        "speaker": "",
        "text": "",
        "audio": null,
        "duration": 0.9,
        "holdSeconds": 0.9,
        "stage": "transition-interrogation",
        "timeCard": "",
        "timeCardCaption": "",
        "timeCardHoldSeconds": 3.5,
        "dipToBlackBefore": false,
        "memoryFlag": ""
      }
    ]
  },
  "FuzzyToMorning": {
    "id": "FuzzyToMorning",
    "canonId": "CS-09",
    "stage": "transition-morning",
    "fadeOutSeconds": 1.2,
    "fadeInSeconds": 0,
    "keepScreenLit": false,
    "endsBlack": true,
    "beats": [
      {
        "speaker": "",
        "text": "",
        "audio": null,
        "duration": 0.9,
        "holdSeconds": 0.9,
        "stage": "transition-morning",
        "timeCard": "",
        "timeCardCaption": "",
        "timeCardHoldSeconds": 3.5,
        "dipToBlackBefore": false,
        "memoryFlag": ""
      }
    ]
  },
  "PriyaScreams": {
    "id": "PriyaScreams",
    "canonId": "CS-10",
    "stage": "morning-reveal",
    "fadeOutSeconds": 0,
    "fadeInSeconds": 0,
    "keepScreenLit": true,
    "endsBlack": false,
    "beats": [
      {
        "speaker": "PRIYA",
        "text": "Guys! Help! Something's happened to Nick! Ivy! Aaron! David! Please, come here!",
        "audio": "/media/vo/PRIYA-001.wav",
        "duration": 8.39,
        "holdSeconds": 4,
        "stage": "morning-reveal",
        "timeCard": "",
        "timeCardCaption": "",
        "timeCardHoldSeconds": 3.5,
        "dipToBlackBefore": false,
        "memoryFlag": "saw_body"
      }
    ]
  },
  "TheyComeDown": {
    "id": "TheyComeDown",
    "canonId": "CS-11",
    "stage": "morning",
    "fadeOutSeconds": 0,
    "fadeInSeconds": 0,
    "keepScreenLit": true,
    "endsBlack": false,
    "beats": [
      {
        "speaker": "",
        "text": "",
        "audio": "/media/sfx/footsteps_stairs.mp3",
        "duration": 3.03,
        "holdSeconds": 1.6,
        "stage": "morning",
        "timeCard": "",
        "timeCardCaption": "",
        "timeCardHoldSeconds": 3.5,
        "dipToBlackBefore": false,
        "memoryFlag": ""
      }
    ]
  },
  "OutIntoTheSnow": {
    "id": "OutIntoTheSnow",
    "canonId": "CS-12",
    "stage": "outside",
    "fadeOutSeconds": 0,
    "fadeInSeconds": 0,
    "keepScreenLit": true,
    "endsBlack": false,
    "beats": [
      {
        "speaker": "PRIYA",
        "text": "What do we do? What do we do?",
        "audio": "/media/vo/PRIYA-002.wav",
        "duration": 2.55,
        "holdSeconds": 2,
        "stage": "outside",
        "timeCard": "",
        "timeCardCaption": "",
        "timeCardHoldSeconds": 3.5,
        "dipToBlackBefore": false,
        "memoryFlag": ""
      },
      {
        "speaker": "IVY",
        "text": "Oh my God. What happened to him? What do we do now?",
        "audio": "/media/vo/IVY-001.wav",
        "duration": 4.63,
        "holdSeconds": 2.5,
        "stage": "outside",
        "timeCard": "",
        "timeCardCaption": "",
        "timeCardHoldSeconds": 3.5,
        "dipToBlackBefore": false,
        "memoryFlag": ""
      },
      {
        "speaker": "AARON",
        "text": "He's freezing. Let's get him inside, onto the sofa by the fire.",
        "audio": "/media/vo/AARON-001.wav",
        "duration": 5.43,
        "holdSeconds": 3,
        "stage": "outside",
        "timeCard": "",
        "timeCardCaption": "",
        "timeCardHoldSeconds": 3.5,
        "dipToBlackBefore": false,
        "memoryFlag": ""
      }
    ]
  },
  "TheCarry": {
    "id": "TheCarry",
    "canonId": "CS-13",
    "stage": "carry",
    "fadeOutSeconds": 0,
    "fadeInSeconds": 0,
    "keepScreenLit": true,
    "endsBlack": false,
    "beats": [
      {
        "speaker": "PRIYA",
        "text": "How did this happen?",
        "audio": "/media/vo/PRIYA-003.wav",
        "duration": 1.59,
        "holdSeconds": 2,
        "stage": "carry",
        "timeCard": "",
        "timeCardCaption": "",
        "timeCardHoldSeconds": 3.5,
        "dipToBlackBefore": false,
        "memoryFlag": ""
      },
      {
        "speaker": "IVY",
        "text": "I don't know. I was upstairs with Aaron.",
        "audio": "/media/vo/IVY-002.wav",
        "duration": 2.47,
        "holdSeconds": 2,
        "stage": "carry",
        "timeCard": "",
        "timeCardCaption": "",
        "timeCardHoldSeconds": 3.5,
        "dipToBlackBefore": false,
        "memoryFlag": ""
      },
      {
        "speaker": "PRIYA",
        "text": "All night?",
        "audio": "/media/vo/PRIYA-004.wav",
        "duration": 1.11,
        "holdSeconds": 1.2,
        "stage": "carry",
        "timeCard": "",
        "timeCardCaption": "",
        "timeCardHoldSeconds": 3.5,
        "dipToBlackBefore": false,
        "memoryFlag": ""
      },
      {
        "speaker": "IVY",
        "text": "Yes. All night.",
        "audio": "/media/vo/IVY-003.wav",
        "duration": 2.15,
        "holdSeconds": 1.5,
        "stage": "carry",
        "timeCard": "",
        "timeCardCaption": "",
        "timeCardHoldSeconds": 3.5,
        "dipToBlackBefore": false,
        "memoryFlag": "heard_ivy_alibi"
      },
      {
        "speaker": "AARON",
        "text": "Priya. Not now.",
        "audio": "/media/vo/AARON-002.wav",
        "duration": 1.99,
        "holdSeconds": 1.2,
        "stage": "carry",
        "timeCard": "",
        "timeCardCaption": "",
        "timeCardHoldSeconds": 3.5,
        "dipToBlackBefore": false,
        "memoryFlag": ""
      },
      {
        "speaker": "PRIYA",
        "text": "The door was locked. Who locked it?",
        "audio": "/media/vo/PRIYA-005.wav",
        "duration": 3.59,
        "holdSeconds": 2,
        "stage": "carry",
        "timeCard": "",
        "timeCardCaption": "",
        "timeCardHoldSeconds": 3.5,
        "dipToBlackBefore": false,
        "memoryFlag": ""
      },
      {
        "speaker": "AARON",
        "text": "Lift on three. One, two, three.",
        "audio": "/media/vo/AARON-003.wav",
        "duration": 4.23,
        "holdSeconds": 1.5,
        "stage": "carry",
        "timeCard": "",
        "timeCardCaption": "",
        "timeCardHoldSeconds": 3.5,
        "dipToBlackBefore": false,
        "memoryFlag": "heard_aaron_deflect"
      }
    ]
  },
  "TheSofa": {
    "id": "TheSofa",
    "canonId": "CS-14",
    "stage": "sofa",
    "fadeOutSeconds": 0,
    "fadeInSeconds": 0,
    "keepScreenLit": true,
    "endsBlack": false,
    "beats": [
      {
        "speaker": "",
        "text": "",
        "audio": "/media/sfx/body_settle_thud.mp3",
        "duration": 1.515,
        "holdSeconds": 2,
        "stage": "sofa",
        "timeCard": "",
        "timeCardCaption": "",
        "timeCardHoldSeconds": 3.5,
        "dipToBlackBefore": false,
        "memoryFlag": ""
      },
      {
        "speaker": "PRIYA",
        "text": "Nick? Nick, can you hear me?",
        "audio": "/media/vo/PRIYA-006.wav",
        "duration": 3.27,
        "holdSeconds": 2.5,
        "stage": "sofa",
        "timeCard": "",
        "timeCardCaption": "",
        "timeCardHoldSeconds": 3.5,
        "dipToBlackBefore": false,
        "memoryFlag": ""
      },
      {
        "speaker": "PRIYA",
        "text": "Police? Our friend is hurt. We found him outside in the snow. Please send someone. Please hurry.",
        "audio": "/media/vo/PRIYA-007.wav",
        "duration": 7.99,
        "holdSeconds": 5,
        "stage": "sofa",
        "timeCard": "",
        "timeCardCaption": "",
        "timeCardHoldSeconds": 3.5,
        "dipToBlackBefore": false,
        "memoryFlag": ""
      }
    ]
  },
  "FuzzyToVerdict": {
    "id": "FuzzyToVerdict",
    "canonId": "CS-15",
    "stage": "transition-interrogation",
    "fadeOutSeconds": 1.2,
    "fadeInSeconds": 0,
    "keepScreenLit": false,
    "endsBlack": true,
    "beats": [
      {
        "speaker": "",
        "text": "",
        "audio": null,
        "duration": 0.9,
        "holdSeconds": 0.9,
        "stage": "transition-interrogation",
        "timeCard": "",
        "timeCardCaption": "",
        "timeCardHoldSeconds": 3.5,
        "dipToBlackBefore": false,
        "memoryFlag": ""
      }
    ]
  },
  "P3Photograph": {
    "id": "P3Photograph",
    "canonId": "P3-PHOTOGRAPH",
    "stage": "interrogation",
    "fadeOutSeconds": 0,
    "fadeInSeconds": 0,
    "keepScreenLit": true,
    "endsBlack": false,
    "beats": [
      {
        "speaker": "SPASSKY",
        "text": "That's what I don't understand.",
        "audio": "/media/vo/SPASSKY-057.wav",
        "duration": 3.19,
        "holdSeconds": 2,
        "stage": "interrogation",
        "timeCard": "",
        "timeCardCaption": "",
        "timeCardHoldSeconds": 3.5,
        "dipToBlackBefore": false,
        "memoryFlag": ""
      },
      {
        "speaker": "SPASSKY",
        "text": "Old friends. An anniversary. Drinks. From the way they tell it, things were going well.",
        "audio": "/media/vo/SPASSKY-058.wav",
        "duration": 7.11,
        "holdSeconds": 5,
        "stage": "interrogation",
        "timeCard": "",
        "timeCardCaption": "",
        "timeCardHoldSeconds": 3.5,
        "dipToBlackBefore": false,
        "memoryFlag": ""
      },
      {
        "speaker": "SPASSKY",
        "text": "So when did it all go wrong?",
        "audio": "/media/vo/SPASSKY-059.wav",
        "duration": 3.19,
        "holdSeconds": 2.4,
        "stage": "interrogation",
        "timeCard": "",
        "timeCardCaption": "",
        "timeCardHoldSeconds": 3.5,
        "dipToBlackBefore": false,
        "memoryFlag": ""
      }
    ]
  },
  "P3AfterGoodYears": {
    "id": "P3AfterGoodYears",
    "canonId": "P3-AFTER-GOOD-YEARS",
    "stage": "interrogation",
    "fadeOutSeconds": 0,
    "fadeInSeconds": 0,
    "keepScreenLit": true,
    "endsBlack": false,
    "beats": [
      {
        "speaker": "SPASSKY",
        "text": "And somewhere between that photograph and sunrise, Nick ended up dead.",
        "audio": "/media/vo/SPASSKY-060.wav",
        "duration": 4.95,
        "holdSeconds": 4.4,
        "stage": "interrogation",
        "timeCard": "",
        "timeCardCaption": "",
        "timeCardHoldSeconds": 3.5,
        "dipToBlackBefore": false,
        "memoryFlag": ""
      },
      {
        "speaker": "SPASSKY",
        "text": "If it wasn't you, David — who killed Nick?",
        "audio": "/media/vo/SPASSKY-061.wav",
        "duration": 3.99,
        "holdSeconds": 3,
        "stage": "interrogation",
        "timeCard": "",
        "timeCardCaption": "",
        "timeCardHoldSeconds": 3.5,
        "dipToBlackBefore": false,
        "memoryFlag": ""
      }
    ]
  },
  "P3WhoDavid": {
    "id": "P3WhoDavid",
    "canonId": "P3-WHO-DAVID",
    "stage": "interrogation",
    "fadeOutSeconds": 0,
    "fadeInSeconds": 0,
    "keepScreenLit": true,
    "endsBlack": false,
    "beats": [
      {
        "speaker": "SPASSKY",
        "text": "Who, David?",
        "audio": "/media/vo/SPASSKY-062.wav",
        "duration": 1.59,
        "holdSeconds": 1.6,
        "stage": "interrogation",
        "timeCard": "",
        "timeCardCaption": "",
        "timeCardHoldSeconds": 3.5,
        "dipToBlackBefore": false,
        "memoryFlag": ""
      }
    ]
  },
  "GoodYears": {
    "id": "GoodYears",
    "canonId": "CS-16A",
    "stage": "good-years",
    "fadeOutSeconds": 0,
    "fadeInSeconds": 0,
    "keepScreenLit": true,
    "endsBlack": false,
    "beats": [
      {
        "speaker": "PRIYA",
        "text": "Fifteen years and you two still act exactly the same.",
        "audio": "/media/vo/PRIYA-014.wav",
        "duration": 4.47,
        "holdSeconds": 2.4,
        "stage": "good-years",
        "timeCard": "1260",
        "timeCardCaption": "Earlier that night",
        "timeCardHoldSeconds": 3.5,
        "dipToBlackBefore": false,
        "memoryFlag": ""
      },
      {
        "speaker": "NICK",
        "text": "He was worse at seventeen.",
        "audio": "/media/vo/NICK-002.wav",
        "duration": 1.99,
        "holdSeconds": 1.6,
        "stage": "good-years",
        "timeCard": "",
        "timeCardCaption": "",
        "timeCardHoldSeconds": 3.5,
        "dipToBlackBefore": false,
        "memoryFlag": ""
      },
      {
        "speaker": "PRIYA",
        "text": "And two years for these two.",
        "audio": "/media/vo/PRIYA-015.wav",
        "duration": 2.23,
        "holdSeconds": 1.8,
        "stage": "good-years",
        "timeCard": "",
        "timeCardCaption": "",
        "timeCardHoldSeconds": 3.5,
        "dipToBlackBefore": false,
        "memoryFlag": ""
      },
      {
        "speaker": "AARON",
        "text": "Barely survived it.",
        "audio": "/media/vo/AARON-004.wav",
        "duration": 1.83,
        "holdSeconds": 1.4,
        "stage": "good-years",
        "timeCard": "",
        "timeCardCaption": "",
        "timeCardHoldSeconds": 3.5,
        "dipToBlackBefore": false,
        "memoryFlag": ""
      },
      {
        "speaker": "PRIYA",
        "text": "To us. Somehow.",
        "audio": "/media/vo/PRIYA-016.wav",
        "duration": 2.79,
        "holdSeconds": 1.4,
        "stage": "good-years",
        "timeCard": "",
        "timeCardCaption": "",
        "timeCardHoldSeconds": 3.5,
        "dipToBlackBefore": false,
        "memoryFlag": ""
      },
      {
        "speaker": "NICK",
        "text": "Unfortunately.",
        "audio": "/media/vo/NICK-003.wav",
        "duration": 1.27,
        "holdSeconds": 1.2,
        "stage": "good-years",
        "timeCard": "",
        "timeCardCaption": "",
        "timeCardHoldSeconds": 3.5,
        "dipToBlackBefore": false,
        "memoryFlag": ""
      },
      {
        "speaker": "NICK",
        "text": "Here. You look fucking freezing.",
        "audio": "/media/vo/NICK-004.wav",
        "duration": 2.63,
        "holdSeconds": 2,
        "stage": "good-years",
        "timeCard": "",
        "timeCardCaption": "",
        "timeCardHoldSeconds": 3.5,
        "dipToBlackBefore": false,
        "memoryFlag": "saw_coat_swap"
      }
    ]
  },
  "WhenItWentWrong": {
    "id": "WhenItWentWrong",
    "canonId": "CS-16B",
    "stage": "went-wrong",
    "fadeOutSeconds": 0,
    "fadeInSeconds": 0,
    "keepScreenLit": true,
    "endsBlack": false,
    "beats": [
      {
        "speaker": "RADIO",
        "text": "…snow storm…",
        "audio": "/media/vo/RADIO-002.wav",
        "duration": 1.51,
        "holdSeconds": 4.5,
        "minimumSeconds": 4.5,
        "stage": "went-wrong",
        "timeCard": "1420",
        "timeCardCaption": "Later. The fire is dying.",
        "timeCardHoldSeconds": 3.5,
        "dipToBlackBefore": false,
        "memoryFlag": ""
      },
      {
        "speaker": "NICK",
        "text": "You've been saying \"after this trip\" for two years.",
        "audio": "/media/vo/NICK-005.wav",
        "duration": 3.51,
        "holdSeconds": 2.6,
        "stage": "went-wrong",
        "timeCard": "",
        "timeCardCaption": "",
        "timeCardHoldSeconds": 3.5,
        "dipToBlackBefore": false,
        "memoryFlag": ""
      },
      {
        "speaker": "AARON",
        "text": "…Two years?",
        "audio": "/media/vo/AARON-005.wav",
        "duration": 2.23,
        "holdSeconds": 1.6,
        "stage": "went-wrong",
        "timeCard": "",
        "timeCardCaption": "",
        "timeCardHoldSeconds": 3.5,
        "dipToBlackBefore": false,
        "memoryFlag": "saw_aaron_learn"
      },
      {
        "speaker": "RADIO",
        "text": "…please stay indoors…",
        "audio": "/media/vo/RADIO-003.wav",
        "duration": 1.99,
        "holdSeconds": 4.5,
        "minimumSeconds": 4.5,
        "stage": "went-wrong",
        "timeCard": "",
        "timeCardCaption": "",
        "timeCardHoldSeconds": 3.5,
        "dipToBlackBefore": false,
        "memoryFlag": ""
      },
      {
        "speaker": "DAVID",
        "text": "You need to tell him.",
        "audio": null,
        "duration": 1.8,
        "holdSeconds": 1.8,
        "stage": "went-wrong",
        "timeCard": "00:50",
        "timeCardCaption": "About an hour later — David and Nick, alone",
        "timeCardHoldSeconds": 3.5,
        "dipToBlackBefore": false,
        "memoryFlag": ""
      },
      {
        "speaker": "NICK",
        "text": "He already knows.",
        "audio": "/media/vo/NICK-006.wav",
        "duration": 2.23,
        "holdSeconds": 1.6,
        "stage": "went-wrong",
        "timeCard": "",
        "timeCardCaption": "",
        "timeCardHoldSeconds": 3.5,
        "dipToBlackBefore": false,
        "memoryFlag": ""
      },
      {
        "speaker": "DAVID",
        "text": "Then say it to his face.",
        "audio": null,
        "duration": 1.8,
        "holdSeconds": 1.8,
        "stage": "went-wrong",
        "timeCard": "",
        "timeCardCaption": "",
        "timeCardHoldSeconds": 3.5,
        "dipToBlackBefore": false,
        "memoryFlag": ""
      },
      {
        "speaker": "NICK",
        "text": "I need some air.",
        "audio": "/media/vo/NICK-007.wav",
        "duration": 1.59,
        "holdSeconds": 1.6,
        "stage": "went-wrong",
        "timeCard": "",
        "timeCardCaption": "",
        "timeCardHoldSeconds": 3.5,
        "dipToBlackBefore": false,
        "memoryFlag": ""
      },
      {
        "speaker": "RADIO",
        "text": "…during these times.",
        "audio": "/media/vo/RADIO-004.wav",
        "duration": 1.51,
        "holdSeconds": 4.5,
        "minimumSeconds": 4.5,
        "stage": "went-wrong",
        "timeCard": "",
        "timeCardCaption": "",
        "timeCardHoldSeconds": 3.5,
        "dipToBlackBefore": false,
        "memoryFlag": ""
      },
      {
        "speaker": "",
        "text": "",
        "audio": null,
        "duration": 4.5,
        "holdSeconds": 4.5,
        "minimumSeconds": 4.5,
        "stage": "went-wrong",
        "timeCard": "",
        "timeCardCaption": "",
        "timeCardHoldSeconds": 3.5,
        "dipToBlackBefore": false,
        "memoryFlag": ""
      }
    ]
  },
  "FlashbackAaron": {
    "id": "FlashbackAaron",
    "canonId": "CS-17A",
    "stage": "accusation-aaron",
    "fadeOutSeconds": 0.5,
    "fadeInSeconds": 0.5,
    "keepScreenLit": false,
    "endsBlack": false,
    "beats": [
      {
        "speaker": "",
        "text": "",
        "audio": "/media/sfx/flashback_bolt_click.mp3",
        "duration": 1.228,
        "holdSeconds": 5,
        "stage": "accusation-aaron",
        "timeCard": "",
        "timeCardCaption": "",
        "timeCardHoldSeconds": 3.5,
        "dipToBlackBefore": false,
        "memoryFlag": ""
      }
    ]
  },
  "FlashbackIvy": {
    "id": "FlashbackIvy",
    "canonId": "CS-17B",
    "stage": "accusation-ivy",
    "fadeOutSeconds": 0.5,
    "fadeInSeconds": 0.5,
    "keepScreenLit": false,
    "endsBlack": false,
    "beats": [
      {
        "speaker": "",
        "text": "",
        "audio": null,
        "duration": 5,
        "holdSeconds": 5,
        "stage": "accusation-ivy",
        "timeCard": "",
        "timeCardCaption": "",
        "timeCardHoldSeconds": 3.5,
        "dipToBlackBefore": false,
        "memoryFlag": ""
      }
    ]
  },
  "FlashbackPriya": {
    "id": "FlashbackPriya",
    "canonId": "CS-17C",
    "stage": "accusation-priya",
    "fadeOutSeconds": 0.5,
    "fadeInSeconds": 0.5,
    "keepScreenLit": false,
    "endsBlack": false,
    "beats": [
      {
        "speaker": "",
        "text": "",
        "audio": "/media/sfx/flashback_glass_clink.mp3",
        "duration": 1.228,
        "holdSeconds": 5,
        "stage": "accusation-priya",
        "timeCard": "",
        "timeCardCaption": "",
        "timeCardHoldSeconds": 3.5,
        "dipToBlackBefore": false,
        "memoryFlag": ""
      }
    ]
  },
  "EndingDavid": {
    "id": "EndingDavid",
    "canonId": "CS-18A",
    "stage": "ending-david",
    "fadeOutSeconds": 0.4,
    "fadeInSeconds": 0.6,
    "keepScreenLit": false,
    "endsBlack": false,
    "beats": [
      {
        "speaker": "SPASSKY",
        "text": "You were the only one who couldn't tell me where you were.",
        "audio": "/media/vo/SPASSKY-053.wav",
        "duration": 3.59,
        "holdSeconds": 4,
        "minimumSeconds": 10,
        "stage": "ending-david",
        "timeCard": "",
        "timeCardCaption": "",
        "timeCardHoldSeconds": 3.5,
        "dipToBlackBefore": false,
        "memoryFlag": ""
      }
    ]
  },
  "EndingAaron": {
    "id": "EndingAaron",
    "canonId": "CS-18B",
    "stage": "ending-aaron",
    "fadeOutSeconds": 0.4,
    "fadeInSeconds": 0.6,
    "keepScreenLit": false,
    "endsBlack": false,
    "beats": [
      {
        "speaker": "SPASSKY",
        "text": "He locked it. You unlocked it. Only one of those was a decision.",
        "audio": "/media/vo/SPASSKY-054.wav",
        "duration": 6.71,
        "holdSeconds": 4,
        "minimumSeconds": 12,
        "stage": "ending-aaron",
        "timeCard": "",
        "timeCardCaption": "",
        "timeCardHoldSeconds": 3.5,
        "dipToBlackBefore": false,
        "memoryFlag": ""
      }
    ]
  },
  "EndingIvy": {
    "id": "EndingIvy",
    "canonId": "CS-18C",
    "stage": "ending-ivy",
    "fadeOutSeconds": 0.4,
    "fadeInSeconds": 0.6,
    "keepScreenLit": false,
    "endsBlack": false,
    "beats": [
      {
        "speaker": "SPASSKY",
        "text": "She agreed with you. That's not the same as it being true.",
        "audio": "/media/vo/SPASSKY-055.wav",
        "duration": 5.43,
        "holdSeconds": 4,
        "minimumSeconds": 10,
        "stage": "ending-ivy",
        "timeCard": "",
        "timeCardCaption": "",
        "timeCardHoldSeconds": 3.5,
        "dipToBlackBefore": false,
        "memoryFlag": ""
      }
    ]
  },
  "EndingPriya": {
    "id": "EndingPriya",
    "canonId": "CS-18D",
    "stage": "ending-priya",
    "fadeOutSeconds": 0.4,
    "fadeInSeconds": 0.6,
    "keepScreenLit": false,
    "endsBlack": false,
    "beats": [
      {
        "speaker": "PRIYA",
        "text": "What happened? Why won't anyone tell me what happened?",
        "audio": "/media/vo/PRIYA-008.wav",
        "duration": 4.39,
        "holdSeconds": 4,
        "minimumSeconds": 5.5,
        "stage": "ending-priya",
        "timeCard": "",
        "timeCardCaption": "",
        "timeCardHoldSeconds": 3.5,
        "dipToBlackBefore": false,
        "memoryFlag": ""
      },
      {
        "speaker": "SPASSKY",
        "text": "She's the one who called us. Sit with that.",
        "audio": "/media/vo/SPASSKY-056.wav",
        "duration": 3.67,
        "holdSeconds": 4,
        "minimumSeconds": 6,
        "stage": "ending-priya",
        "timeCard": "",
        "timeCardCaption": "",
        "timeCardHoldSeconds": 3.5,
        "dipToBlackBefore": false,
        "memoryFlag": ""
      }
    ]
  }
});

export const SEQUENCES = Object.freeze({
  truthReveal: TRUTH_SEQUENCE,
  "start": [
    "Wake",
    "SpasskyAnswer",
    "FuzzyToNight"
  ],
  "wakeNight": [
    "StandFromChair",
    "NightArgument"
  ],
  "radio": [
    "RadioClears",
    "SomeoneLeft"
  ],
  "nightEnd": [
    "CallForNick",
    "FuzzyToInterrogation"
  ],
  "morningStart": [
    "FuzzyToMorning",
    "PriyaScreams",
    "TheyComeDown"
  ],
  "morningEnd": [
    "OutIntoTheSnow",
    "TheCarry",
    "TheSofa",
    "FuzzyToVerdict"
  ],
  "verdictMemory": [
    "P3Photograph",
    "GoodYears",
    "P3AfterGoodYears",
    "WhenItWentWrong",
    "P3WhoDavid"
  ],
  "accusationAaron": [
    "FlashbackAaron"
  ],
  "accusationIvy": [
    "FlashbackIvy"
  ],
  "accusationPriya": [
    "FlashbackPriya"
  ],
  "endingDavid": [
    "EndingDavid"
  ],
  "endingAaron": [
    "EndingAaron"
  ],
  "endingIvy": [
    "EndingIvy"
  ],
  "endingPriya": [
    "EndingPriya"
  ]
});

export const CINEMATIC_SOURCE_FILES = Object.freeze([
  "Assets/_Project/Art/Audio/SFX/body_settle_thud.mp3",
  "Assets/_Project/Art/Audio/SFX/chair_creak.mp3",
  "Assets/_Project/Art/Audio/SFX/door_latch_close.mp3",
  "Assets/_Project/Art/Audio/SFX/flashback_bolt_click.mp3",
  "Assets/_Project/Art/Audio/SFX/flashback_glass_clink.mp3",
  "Assets/_Project/Art/Audio/SFX/footsteps_stairs.mp3",
  "Assets/_Project/Art/Audio/SFX/wind_gust_roar.mp3",
  "Assets/_Project/Art/Audio/VO/Production/AARON-001.wav",
  "Assets/_Project/Art/Audio/VO/Production/AARON-002.wav",
  "Assets/_Project/Art/Audio/VO/Production/AARON-003.wav",
  "Assets/_Project/Art/Audio/VO/Production/AARON-004.wav",
  "Assets/_Project/Art/Audio/VO/Production/AARON-005.wav",
  "Assets/_Project/Art/Audio/VO/Production/IVY-001.wav",
  "Assets/_Project/Art/Audio/VO/Production/IVY-002.wav",
  "Assets/_Project/Art/Audio/VO/Production/IVY-003.wav",
  "Assets/_Project/Art/Audio/VO/Production/NICK-001.wav",
  "Assets/_Project/Art/Audio/VO/Production/NICK-002.wav",
  "Assets/_Project/Art/Audio/VO/Production/NICK-003.wav",
  "Assets/_Project/Art/Audio/VO/Production/NICK-004.wav",
  "Assets/_Project/Art/Audio/VO/Production/NICK-005.wav",
  "Assets/_Project/Art/Audio/VO/Production/NICK-006.wav",
  "Assets/_Project/Art/Audio/VO/Production/NICK-007.wav",
  "Assets/_Project/Art/Audio/VO/Production/PRIYA-001.wav",
  "Assets/_Project/Art/Audio/VO/Production/PRIYA-002.wav",
  "Assets/_Project/Art/Audio/VO/Production/PRIYA-003.wav",
  "Assets/_Project/Art/Audio/VO/Production/PRIYA-004.wav",
  "Assets/_Project/Art/Audio/VO/Production/PRIYA-005.wav",
  "Assets/_Project/Art/Audio/VO/Production/PRIYA-006.wav",
  "Assets/_Project/Art/Audio/VO/Production/PRIYA-007.wav",
  "Assets/_Project/Art/Audio/VO/Production/PRIYA-008.wav",
  "Assets/_Project/Art/Audio/VO/Production/PRIYA-014.wav",
  "Assets/_Project/Art/Audio/VO/Production/PRIYA-015.wav",
  "Assets/_Project/Art/Audio/VO/Production/PRIYA-016.wav",
  "Assets/_Project/Art/Audio/VO/Production/RADIO-001.wav",
  "Assets/_Project/Art/Audio/VO/Production/RADIO-002.wav",
  "Assets/_Project/Art/Audio/VO/Production/RADIO-003.wav",
  "Assets/_Project/Art/Audio/VO/Production/RADIO-004.wav",
  "Assets/_Project/Art/Audio/VO/Production/SPASSKY-001.wav",
  "Assets/_Project/Art/Audio/VO/Production/SPASSKY-002.wav",
  "Assets/_Project/Art/Audio/VO/Production/SPASSKY-003.wav",
  "Assets/_Project/Art/Audio/VO/Production/SPASSKY-005.wav",
  "Assets/_Project/Art/Audio/VO/Production/SPASSKY-053.wav",
  "Assets/_Project/Art/Audio/VO/Production/SPASSKY-054.wav",
  "Assets/_Project/Art/Audio/VO/Production/SPASSKY-055.wav",
  "Assets/_Project/Art/Audio/VO/Production/SPASSKY-056.wav",
  "Assets/_Project/Art/Audio/VO/Production/SPASSKY-057.wav",
  "Assets/_Project/Art/Audio/VO/Production/SPASSKY-058.wav",
  "Assets/_Project/Art/Audio/VO/Production/SPASSKY-059.wav",
  "Assets/_Project/Art/Audio/VO/Production/SPASSKY-060.wav",
  "Assets/_Project/Art/Audio/VO/Production/SPASSKY-061.wav",
  "Assets/_Project/Art/Audio/VO/Production/SPASSKY-062.wav"
]);

/** Bounded, cancellable authored playback. onBeat stages the actual XR scene. */
export function createCinematicPlayer({
  onBeat = () => {}, onFinish = () => {}, onError = () => {}, onAudioState = () => {},
  AudioClass = typeof Audio === 'undefined' ? null : Audio,
  setTimer = setTimeout, clearTimer = clearTimeout, maxBeatSeconds = 45,
  now = () => (typeof performance === 'undefined' ? Date.now() : performance.now()),
} = {}) {
  let current = null;
  let generation = 0;
  const limit = Math.max(0.05, Math.min(90, Number(maxBeatSeconds) || 45));
  const report = (detail) => { try { onError(detail); } catch { /* Presentation errors cannot stall the case. */ } };
  function stopAudio(run) {
    if (!run.audio) return;
    try { onAudioState({active:false,...run.audioContext}); } catch { /* UI cannot trap playback. */ }
    run.audio.onended = null;
    run.audio.onerror = null;
    run.audio.onplaying = null;
    try { run.audio.pause(); } catch { /* An unavailable audio device is recoverable. */ }
    run.audio = null;
  }
  function halt(status) {
    const run = current;
    if (!run) return false;
    run.status = status;
    stopAudio(run);
    run.settle?.();
    return true;
  }
  function cancel() {
    generation += 1;
    const stopped = halt('cancelled');
    current = null;
    return stopped;
  }
  function playBeat(run, beat, context) {
    return new Promise((resolve) => {
      let done = false;
      let timer = null;
      let remainingMs = 0;
      let deadline = 0;
      let activeStarted = now(), elapsedBeforePause = 0, audioEnded = false;
      const elapsed = () => elapsedBeforePause + (run.paused ? 0 : now() - activeStarted);
      const minimumMs = Math.max(0, Math.min(limit, Number(beat.minimumSeconds) || 0)) * 1000;
      const finish = () => {
        if (done) return;
        // A timer/audio-ended event already queued before pause must not advance it.
        if (run.paused && run.status === 'playing') { remainingMs = 0; return; }
        done = true;
        if (timer !== null) clearTimer(timer);
        stopAudio(run);
        run.settle = null;
        run.pauseCurrent = null;
        run.resumeCurrent = null;
        resolve();
      };
      const schedule = (seconds) => {
        if (timer !== null) clearTimer(timer);
        const finite = Number.isFinite(Number(seconds)) ? Number(seconds) : 2.5;
        remainingMs = Math.max(0.05, Math.min(limit, finite)) * 1000;
        deadline = now() + remainingMs;
        timer = run.paused ? null : setTimer(finish, remainingMs);
      };
      run.settle = finish;
      run.pauseCurrent = () => {
        if (done) return;
        elapsedBeforePause += Math.max(0, now() - activeStarted);
        if (timer !== null) {
          remainingMs = Math.max(0, deadline - now());
          clearTimer(timer);
          timer = null;
        }
        try { run.audio?.pause(); } catch { /* Timer still pauses. */ }
      };
      run.resumeCurrent = () => {
        if (done) return;
        activeStarted = now();
        deadline = now() + remainingMs;
        timer = setTimer(finish, Math.max(1, remainingMs, minimumMs - elapsedBeforePause));
        if (run.audio && !audioEnded) {
          try { const started = run.audio.play(); if (started?.catch) started.catch(fallback); }
          catch (error) { fallback(error); }
        }
      };
      // The watchdog starts before audio.play(), so even an unresolved play promise ends.
      schedule(beat.audio ? Math.min(limit,8) : Math.max(Number(beat.duration) || Number(beat.holdSeconds) || 2.5, minimumMs / 1000));
      const fallback = (error) => {
        if (done || current !== run || run.generation !== generation) return;
        report({ type: 'audio', ...context, message: String(error?.message || 'Audio unavailable; captions continue.') });
        stopAudio(run);
        schedule(Math.max(beat.holdSeconds || beat.duration || 2.5,(minimumMs-elapsed())/1000));
      };
      try {
        onBeat({ ...beat, ...context });
      } catch (error) {
        report({ type: 'staging', ...context, message: String(error?.message || error) });
      }
      if (!beat.audio || !AudioClass) return;
      try {
        const audio = new AudioClass(beat.audio);
        run.audio = audio;
        audio.volume = audioGain(beat.audio);
        audio.preload = 'auto';
        audio.onended = () => {
          audioEnded = true;
          try { onAudioState({active:false,speaker:beat.speaker,...context}); } catch {}
          const remaining = minimumMs - elapsed();
          if (remaining > 0) schedule(remaining / 1000);
          else finish();
        };
        audio.onplaying = () => {
          if (done || run.paused || current !== run) return;
          try { onAudioState({active:true,speaker:beat.speaker,...context}); } catch {}
          const duration = Number.isFinite(audio.duration) ? audio.duration : Number(beat.duration) || 2.5;
          schedule(Math.max(duration - (audio.currentTime || 0) + 2, (minimumMs - elapsed()) / 1000));
        };
        audio.onerror = fallback;
        run.audioContext={speaker:beat.speaker,...context};
        const started = audio.play();
        if (started?.catch) started.catch(fallback);
      } catch (error) { fallback(error); }
    });
  }
  async function play(hookOrIds) {
    const ids = Array.isArray(hookOrIds)
      ? [...hookOrIds]
      : SEQUENCES[hookOrIds] ? [...SEQUENCES[hookOrIds]] : [hookOrIds];
    if (ids.some(id => !CINEMATICS[id])) throw new RangeError('Unknown cinematic sequence.');
    cancel();
    const run = { generation: ++generation, status: 'playing', paused: false, audio: null, settle: null, pauseCurrent: null, resumeCurrent: null };
    current = run;
    const beatCount = ids.reduce((sum, id) => sum + CINEMATICS[id].beats.length, 0);
    let step = 0;
    for (const id of ids) {
      const scene = CINEMATICS[id];
      for (let i = 0; i < scene.beats.length; i += 1) {
        if (run.status !== 'playing' || current !== run || generation !== run.generation) break;
        await playBeat(run, scene.beats[i], {
          cutscene: id, canonId: scene.canonId, beatIndex: i,
          beatCount: scene.beats.length, step: step++, totalSteps: beatCount,
          fadeOutSeconds: scene.fadeOutSeconds, fadeInSeconds: scene.fadeInSeconds,
          keepScreenLit: scene.keepScreenLit, endsBlack: scene.endsBlack,
        });
      }
      if (run.status !== 'playing' || current !== run || generation !== run.generation) break;
    }
    if (run.status === 'playing') run.status = generation === run.generation ? 'completed' : 'cancelled';
    stopAudio(run);
    if (current === run) current = null;
    const result = { status: run.status, sequence: ids, playedBeats: step };
    if (run.status !== 'cancelled') {
      try { onFinish(result); } catch (error) { report({ type: 'finish', message: String(error?.message || error) }); }
    }
    return result;
  }
  return {
    play, skip: () => halt('skipped'), cancel,
    pause() {
      if (!current || current.status !== 'playing' || current.paused) return false;
      current.paused = true;
      current.pauseCurrent?.();
      return true;
    },
    resume() {
      if (!current || current.status !== 'playing' || !current.paused) return false;
      current.paused = false;
      current.resumeCurrent?.();
      return true;
    },
    get paused() { return Boolean(current?.paused); },
    get playing() { return current?.status === 'playing'; },
  };
}
