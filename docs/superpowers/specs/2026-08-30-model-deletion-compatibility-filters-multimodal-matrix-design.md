# Design Specification: Model Deletion, Hardware Compatibility Filters, and Multimodal Matrix

**Date:** 2026-08-30  
**Status:** Approved  
**Author:** Antigravity Team  

---

## 1. Overview and Problem Statement

LocalLLMServerManager currently provides model discovery, execution orchestration, and "Can I Run It" ambient hardware compatibility badges across multiple tabs. However:
1. **Model Deletion is Missing:** Users cannot delete installed Ollama models or downloaded files (ComfyUI / Forge / audio / 3D model weights) directly from the manager UI.
2. **Hardware Compatibility Filtering is Missing:** While each model card shows a `FitBadge` (🟢 Full VRAM, 🟡 Partial Offload, 🟠 CPU Only, 🔴 Won't Fit), users cannot filter search results by these verdicts to only see models their GPU / RAM can run.
3. **Multimodal & Flexible I/O Selection is Missing:** Search filters only support single hardcoded categories (e.g. `gguf`, `text-to-video`, `text-to-speech`). Users cannot specify multimodal capabilities (such as Vision-Language Models with Text + Image inputs producing Text outputs, or Image-to-Video, Audio-to-Text) or select multiple input and output types simultaneously.

---

## 2. Core Architectural Components

### 2.1 Backend Endpoint: `DELETE /api/models/delete`
* **Route:** `DELETE /api/models/delete`
* **Request Contract:**
  ```json
  {
    "target": "llama3.2:latest",
    "type": "ollama" // or "file", "checkpoint", "diffusion", "audio", "3d"
  }
  ```
* **Ollama Deletion Execution:**
  * Sends `DELETE` HTTP request to `http://127.0.0.1:11434/api/delete` with payload `{"name": target}`.
* **Local Disk Deletion Execution:**
  * Uses `Program.IsSafePath(target)` to strictly validate path boundaries against dangerous path traversal attacks (`..`, system directories, invalid characters).
  * Deletes the target file from the appropriate storage directory (`ComfyUI/models/diffusion_models`, `models/checkpoints`, `audio/stt`, `audio/engines`, `models/tts`, `models/3d`, `models/Lora`).
  * Returns `{ "status": "success", "target": target }`.

### 2.2 Service Extensions
* **`IOllamaModelService` & `OllamaModelService`:**
  * `Task<bool> DeleteModelAsync(string apiBase, string modelName, HttpClient http)`
  * `Task<bool> DeleteLocalModelFileAsync(string apiBase, string filePath, HttpClient http)`
* **`IHuggingFaceSearchService` & `HuggingFaceSearchService`:**
  * Support multiple pipeline tags or combined query parameters for multimodal discovery.
  * Helper to resolve appropriate pipeline tags from selected Input and Output modalities.

---

## 3. Multimodal Input/Output Matrix & Modality Mapping

### 3.1 Supported Modalities
* **Inputs:** `Text`, `Image`, `Audio`, `Video`
* **Outputs:** `Text`, `Image`, `Audio`, `Video`, `3D`

### 3.2 Modality to Hugging Face Pipeline Tag Mapping

| Input Modality | Output Modality | Hugging Face Pipeline Tag(s) / Search Filters | Example Models / Tasks |
| :--- | :--- | :--- | :--- |
| `Text` | `Text` | `text-generation`, `text2text-generation`, `gguf` | Llama 3.3, Qwen 2.5, DeepSeek R1 |
| `Text` + `Image` | `Text` | `image-text-to-text`, `image-to-text`, `visual-question-answering` | Llama 3.2 Vision, Qwen2-VL, Pixtral |
| `Text` | `Image` | `text-to-image` | Flux.1, SDXL, Stable Diffusion 3.5 |
| `Image` (+ `Text`) | `Image` | `image-to-image` | ControlNet, Inpainting, InstructPix2Pix |
| `Text` | `Video` | `text-to-video` | Wan 2.2, LTX-Video, HunyuanVideo |
| `Image` (+ `Text`) | `Video` | `image-to-video` | AnimateDiff, SVD, Wan I2V |
| `Text` | `Audio` | `text-to-speech`, `text-to-audio` | Kokoro TTS, XTTS-v2, MusicGen |
| `Audio` | `Text` | `automatic-speech-recognition` | Whisper Large-v3, Faster-Whisper |
| `Text` / `Image` | `3D` | `text-to-3d`, `image-to-3d` | TRELLIS, Hunyuan3D-2 |

### 3.3 Quick Presets
* **`🌟 Multimodal / VLM`:** Activates Inputs: `[Text, Image]`, Output: `[Text]`.
* **`🦙 Text LLM`:** Activates Input: `[Text]`, Output: `[Text]`.
* **`🎨 Text-to-Image`:** Activates Input: `[Text]`, Output: `[Image]`.
* **`🎬 Video Gen`:** Activates Inputs: `[Text, Image]`, Output: `[Video]`.
* **`🔊 Speech & Audio`:** Activates Inputs: `[Text, Audio]`, Outputs: `[Text, Audio]`.
* **`📦 3D Mesh`:** Activates Inputs: `[Text, Image]`, Output: `[3D]`.

---

## 4. Hardware Compatibility ("Can I Run It") Filtering

### 4.1 Filter Verdict Chips
In `HuggingFaceSearchViewModel`, `CivitaiSearchViewModel`, and `OllamaLibraryViewModel`:
* **Chips:**
  * 🟢 `Full VRAM` (`FitVerdict.FullVram`)
  * 🟡 `Partial Offload` (`FitVerdict.PartialOffload`)
  * 🟠 `CPU Only` (`FitVerdict.CpuOnly`)
  * 🔴 `Won't Fit` (`FitVerdict.OutOfMemory`)
* **Behavior:**
  * Stored in active verdict boolean flags (`IsFullVramActive`, `IsPartialOffloadActive`, `IsCpuOnlyActive`, `IsOomActive`).
  * By default, all verdicts are active.
  * Filtered collections (`FilteredResults` / `FilteredInstalledModels`) update reactively without re-fetching from the network.

---

## 5. ViewModels and UI Architecture

### 5.1 `OllamaLibraryViewModel`
* Added `DeleteModelCommand(OllamaModelItem item)`:
  * Prompts/triggers delete via `_ollamaModelService.DeleteModelAsync`.
  * Removes item from `InstalledModels` collection.
  * Shows a `ToastType.Success` toast notification.
* Filter chips for `FitVerdict` compatibility.
* `FilteredInstalledModels` observable collection bound to `OllamaModelsTabControl.axaml`.

### 5.2 `HuggingFaceSearchViewModel`
* `SelectedInputModalities` & `SelectedOutputModalities` multi-select state.
* `ToggleInputModalityCommand(string modality)` & `ToggleOutputModalityCommand(string modality)`.
* `ApplyPresetCommand(string presetName)`.
* `ActiveFitVerdicts` for hardware compatibility filtering.
* `FilteredHuggingFaceResults` observable collection bound to `HuggingFaceTabControl.axaml`.

### 5.3 `CivitaiSearchViewModel`
* `ActiveFitVerdicts` for hardware compatibility filtering.
* `FilteredCivitaiResults` observable collection bound to `CivitaiTabControl.axaml`.

---

## 6. Testing and Verification Plan

1. **Backend Endpoint Tests:**
   * Test `DELETE /api/models/delete` with Ollama target (mocked HTTP response).
   * Test `DELETE /api/models/delete` with safe local files and verify path validation blocks unsafe traversal paths.
2. **ViewModel Unit Tests:**
   * Verify `OllamaLibraryViewModel.DeleteModelCommand` removes model and triggers toast.
   * Verify `HuggingFaceSearchViewModel` filtering filters items based on `FitVerdict` toggle chips.
   * Verify Multimodal Input/Output matrix translates to valid Hugging Face query pipeline tags.
   * Verify `CivitaiSearchViewModel` filtering by `FitVerdict`.
3. **Linting and Typechecking:**
   * Run `npm run lint` and `npx tsc --noEmit`.
   * Run `dotnet test`.
