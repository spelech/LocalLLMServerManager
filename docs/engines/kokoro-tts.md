# Kokoro TTS Engine Integration

Kokoro TTS is an ultra-fast, lightweight text-to-speech model with 82 million parameters. The model generates human-quality voice audio locally in under 100 milliseconds.

Local LLM Server Manager integrates Kokoro TTS on port `8880` and exposes an OpenAI-compatible speech proxy on port `5246`.

---

## Engine Overview

* **Default Backend Port**: `:8880` (`http://127.0.0.1:8880`)
* **Proxy Endpoint**: `POST http://127.0.0.1:5246/v1/audio/speech`
* **Model Footprint**: ~350 MB (`kokoro-v1.0.onnx` + `voices-v1.0.bin`)
* **Hardware Requirements**: Runs efficiently on CPU or GPU with less than 1 GB VRAM
* **Voice Catalog**: 50+ built-in voices covering multiple accents and languages

```mermaid
graph LR
    Client["OpenAI Client / WebUI / Assistant"] -->|"POST /v1/audio/speech"| Proxy["Server Manager Proxy (:5246)"]
    Proxy --> Kokoro["Kokoro-FastAPI Engine (:8880)"]
    Kokoro --> ONNX["ONNX Runtime (CPU / CUDA)"]
    ONNX --> Audio["WAV / MP3 Stream"]
    Audio --> Studio["Audio Studio Waveform Player"]
```

---

## Setting Up Kokoro TTS

You can install Kokoro TTS through the manager's modular component system:

1. Open the **Local LLM Server Manager** dashboard.
2. Select the **Settings** tab.
3. Locate the **Modular Feature Packs** section.
4. Find the **Audio Feature Pack (`ext_audio`)**.
5. Click **Install Audio Pack**.
6. The manager executes the installation script (`scripts/setup_kokoro_tts.ps1`).
7. The installer sets up Python dependencies (`kokoro-onnx`, `soundfile`, `fastapi`, `uvicorn`) and downloads model weights (~350 MB).
8. Once installed, click **Boot Audio Engine**.
9. Verify the green **Audio Engine** status badge in the header.

> [!NOTE]
> If you already run Kokoro-FastAPI independently, specify your custom executable path or Docker command in the **Settings** tab.

---

## OpenAI-Compatible Speech Proxy

Local LLM Server Manager exposes a standard OpenAI `/v1/audio/speech` endpoint. Third-party applications can use local speech synthesis without code modifications.

### API Endpoint Specification

* **URL**: `http://localhost:5246/v1/audio/speech`
* **Method**: `POST`
* **Headers**: `Content-Type: application/json`

### Sample JSON Request Body

```json
{
  "model": "kokoro",
  "input": "Local LLM Server Manager provides fast, private speech synthesis.",
  "voice": "af_heart",
  "response_format": "mp3",
  "speed": 1.0
}
```

### Supported Parameters

| Parameter | Type | Required | Description | Default |
| :--- | :--- | :--- | :--- | :--- |
| `model` | string | Yes | Model identifier (`kokoro` or `tts-1`). | `kokoro` |
| `input` | string | Yes | Text string to synthesize into speech. | N/A |
| `voice` | string | Yes | Target voice identifier (see voice catalog). | `af_heart` |
| `response_format` | string | No | Output audio container (`mp3`, `wav`, `opus`, `flac`). | `mp3` |
| `speed` | number | No | Speaking speed multiplier (`0.5` to `2.0`). | `1.0` |

### Testing with cURL

Open your terminal and run this command to generate speech:

```bash
curl -X POST http://localhost:5246/v1/audio/speech \
  -H "Content-Type: application/json" \
  -d "{\"model\":\"kokoro\",\"input\":\"Welcome to local speech synthesis!\",\"voice\":\"af_heart\"}" \
  --output welcome.mp3
```

> [!TIP]
> Configure Open WebUI or LibreChat to use `http://localhost:5246/v1` as their OpenAI TTS endpoint. Your chat assistants will speak responses aloud with zero cloud latency.

---

## Voice Catalog (50+ Voices)

Kokoro uses standard naming prefixes to organize voice traits:
* **First character (Language/Accent)**: `a` = American English, `b` = British English, `j` = Japanese, `z` = Mandarin Chinese, `e` = Spanish, `f` = French, `h` = Hindi, `i` = Italian.
* **Second character (Gender)**: `f` = Female, `m` = Male.

### Recommended English Voices

| Voice ID | Accent | Gender | Description | Quality Rank |
| :--- | :--- | :--- | :--- | :--- |
| `af_heart` | American | Female | Warm, highly expressive flagship voice | Top Pick |
| `af_bella` | American | Female | Clear, energetic, professional | High |
| `af_nicole` | American | Female | Calm, relaxed conversational tone | High |
| `af_sky` | American | Female | Bright, modern assistant voice | High |
| `am_adam` | American | Male | Deep, clear narration voice | Top Pick |
| `am_echo` | American | Male | Balanced, smooth instructional voice | High |
| `am_fenrir` | American | Male | Resonant, authoritative tone | High |
| `am_michael` | American | Male | Natural conversational pacing | High |
| `bf_alice` | British | Female | Formal, articulate presentation tone | Top Pick |
| `bf_emma` | British | Female | Gentle, friendly British accent | High |
| `bm_daniel` | British | Male | Crisp, documentary narration style | Top Pick |
| `bm_george` | British | Male | Warm, distinguished tone | High |

### International Voices

Kokoro also includes multilingual voices:
* **Japanese**: `jf_alpha`, `jf_gongitsune`, `jm_kumo`
* **Mandarin Chinese**: `zf_xiaobei`, `zf_xiaoni`, `zm_yunjian`
* **Spanish**: `ef_dora`, `em_alex`, `em_santa`
* **French**: `ff_siwis`, `fm_luc`
* **Italian**: `if_sara`, `im_nicola`
* **Hindi**: `hf_alpha`, `hm_omega`

> [!IMPORTANT]
> The `af_heart` voice offers the highest expressive fidelity. Select `af_heart` as your default voice for conversational agents.

---

## Using Kokoro in Audio Studio

1. Open the **Studio** tab in the dashboard.
2. Select the **Audio & Speech** sub-tab.
3. Type or paste text into the **Speech Input** box.
4. Select your voice from the **Voice Catalog** dropdown.
5. Adjust the **Speaking Speed** slider.
6. Click **Generate Speech**.
7. Listen to the generated audio on the interactive waveform player.
8. Click **Download Audio** to save the file.

---

## Related Documentation

* [Engines & VRAM Orchestrator Overview](./index.md)
* [Multimodal Audio & Music Studio Guide](../studio/audio-and-music.md)
