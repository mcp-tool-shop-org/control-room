<p align="center">
  <a href="README.ja.md">日本語</a> | <a href="README.zh.md">中文</a> | <a href="README.es.md">Español</a> | <a href="README.fr.md">Français</a> | <a href="README.hi.md">हिन्दी</a> | <a href="README.it.md">Italiano</a> | <a href="README.pt-BR.md">Português (BR)</a>
</p>

<p align="center">
  <img src="https://raw.githubusercontent.com/mcp-tool-shop-org/control-room/main/assets/logo-control-room.png" alt="Control Room" width="400">
</p>

<p align="center">
  <a href="https://github.com/mcp-tool-shop-org/control-room/actions/workflows/ci.yml"><img src="https://github.com/mcp-tool-shop-org/control-room/actions/workflows/ci.yml/badge.svg" alt="CI"></a>
  <a href="LICENSE"><img src="https://img.shields.io/badge/License-MIT-yellow" alt="MIT License"></a>
  <a href="https://mcp-tool-shop-org.github.io/control-room/"><img src="https://img.shields.io/badge/Landing_Page-live-blue" alt="Landing Page"></a>
</p>

**एक ऐसा डेस्कटॉप एप्लिकेशन जो स्थानीय रूप से काम करता है और स्क्रिप्ट, सर्वर और कार्यों को प्रबंधित और निष्पादित करने में मदद करता है, साथ ही यह पूरी तरह से निगरानी करने की क्षमता प्रदान करता है।**

## कंट्रोल रूम क्या है?

कंट्रोल रूम आपकी स्क्रिप्ट को ऐसे कार्यों में बदल देता है जिन्हें देखा जा सकता है और जिन्हें दोहराया जा सकता है। टर्मिनल में `python train.py --config=prod` चलाने और सर्वोत्तम परिणाम की उम्मीद करने के बजाय, आपको यह मिलता है:

- **विश्वसनीय निष्पादन रिकॉर्ड** — प्रत्येक निष्पादन को stdout/stderr, एग्जिट कोड, समय और अन्य महत्वपूर्ण जानकारी के साथ लॉग किया जाता है।
- **त्रुटि विश्लेषण** — बार-बार होने वाली त्रुटियों को समूहीकृत किया जाता है और विभिन्न निष्पादों में ट्रैक किया जाता है।
- **प्रोफाइल** — प्रत्येक स्क्रिप्ट के लिए पूर्वनिर्धारित तर्क/पर्यावरण संयोजनों को परिभाषित करें (जैसे, प्रारंभिक परीक्षण, पूर्ण परीक्षण, डिबग)।
- **कमांड पैलेट** — कीबोर्ड के माध्यम से निष्पादन, जिसमें फ़ज़ी खोज की सुविधा है।

## विशेषताएं

### प्रोफाइल (नया!)

प्रत्येक स्क्रिप्ट के लिए कई निष्पादन कॉन्फ़िगरेशन परिभाषित करें:

```
Thing: "train-model"
├── Default          (no args)
├── Smoke            --epochs=1 --subset=100
├── Full             --epochs=50 --wandb
└── Debug            --verbose --no-cache  DEBUG=1
```

कमांड पैलेट प्रत्येक प्रोफाइल को एक अलग कार्रवाई के रूप में दिखाता है। किसी विफल निष्पादन को पुनः चलाने पर, वही प्रोफाइल उपयोग किया जाता है जो विफल हुआ था।

### त्रुटि समूह

त्रुटियों को उनके हस्ताक्षर के आधार पर वर्गीकृत किया जाता है। "त्रुटि" पृष्ठ पर, बार-बार होने वाली समस्याओं को उनके हस्ताक्षर के आधार पर समूहीकृत किया जाता है, साथ ही उनकी पुनरावृत्ति की संख्या और पहली/अंतिम देखे गए समय के स्टैम्प भी प्रदर्शित होते हैं।

### टाइमलाइन

सभी निष्पादों को कालानुक्रमिक क्रम में देखें। किसी विशिष्ट त्रुटि के सभी उदाहरणों को देखने के लिए, त्रुटि हस्ताक्षर के आधार पर फ़िल्टर करें।

### ज़िप एक्सपोर्ट

किसी भी निष्पादन को ज़िप फ़ाइल के रूप में एक्सपोर्ट करें, जिसमें निम्नलिखित शामिल होंगे:
- `run-info.json` — सभी मेटाडेटा (तर्क, पर्यावरण, समय, उपयोग किया गया प्रोफाइल)
- `stdout.txt` / `stderr.txt` — पूर्ण आउटपुट
- `events.jsonl` — मशीन-पठनीय इवेंट स्ट्रीम
- `artifacts/` — एकत्र किए गए सभी डेटा

## तकनीकी जानकारी

- **.NET MAUI** — क्रॉस-प्लेटफ़ॉर्म डेस्कटॉप यूआई (विंडोज पर केंद्रित)
- **SQLite (WAL मोड)** — स्थानीय डेटा संग्रहण
- **CommunityToolkit.Mvvm** — सोर्स जेनरेटर के साथ MVVM

## शुरुआत कैसे करें

### आवश्यकताएं

- .NET 10 SDK
- विंडोज 10/11

### बिल्ड करें

```bash
dotnet restore
dotnet build
```

### चलाएं

```bash
dotnet run --project ControlRoom.App
```

## परियोजना संरचना

```
ControlRoom/
├── ControlRoom.Domain/        # Domain models (Thing, Run, ThingConfig, etc.)
├── ControlRoom.Application/   # Use cases (RunLocalScript, etc.)
├── ControlRoom.Infrastructure/ # SQLite storage, queries
└── ControlRoom.App/           # MAUI UI layer
```

## लाइसेंस

MIT — देखें [LICENSE](LICENSE)

## योगदान

योगदान का स्वागत है! कृपया प्रस्तावित परिवर्तनों पर चर्चा करने के लिए पहले एक मुद्दा खोलें।
