# Guide to Community LoRAs & Art Styles

This guide explains how to use **LoRAs (Low-Rank Adaptations)** to generate specific art styles, characters, and aesthetics using the **LocalLLMServerManager** dashboard.

## What is a LoRA?
Instead of downloading massive 6GB checkpoint models for every single art style, you use your large base models (like *Pony V6 XL* or *Juggernaut X*) and attach a small ~100MB LoRA to it. The LoRA acts like an "override chip" that forces the model into a specific style, character, or concept.

---

## How to Download LoRAs via the Manager Dash

You do not need to use an external browser to get these! 

1. Open your manager UI at **`http://localhost:5246`**.
2. Click on the **Stable Diffusion (CivitAI)** tab.
3. In the search filters, set the **Type** to **LoRA**.
4. Sort by **Highest Rated** or **Most Downloaded**.
5. Type any of the search terms below into the search bar.
6. Click Download. The file will automatically save to your shared `D:\AI\models\loras` directory.

---

## Recommended Search Terms by Style

### 1. Retro & Video Game Styles
*   **"Pixel Art XL"** or **"16-bit"**: Incredible for generating authentic game sprites and retro backgrounds.
*   **"PS1 Graphics"** or **"Retro 3D"**: Recreates the jagged, low-poly, pixelated-texture aesthetic of 1998 PlayStation games.
*   **"Gameboy"**: Forces the entire generation into a classic 4-color green dot-matrix style.

### 2. Cartoons & Anime
*   **"Cel Shaded"** or **"Studio Ghibli"**: For flat, beautifully colored 2D anime styles.
*   **"SpongeBob"**: Specifically trained on the show's aesthetic and characters.
*   **"Paw Patrol"**: You will find character-specific LoRAs for Chase, Marshall, etc., allowing you to put them in custom scenarios.
*   *(Note: The **Pony V6 XL** base model you downloaded is already heavily optimized for cartoon/anime styles, so pairing these LoRAs with Pony V6 XL yields the best results).*

### 3. Realistic & Enhancers
*   **"Detail Tweaker XL"** or **"Add More Details"**: A utility LoRA that simply makes realistic photos look incredibly crisp, adding pores, fabric textures, and micro-details.
*   **"Cinematic Lighting"**: Forces dramatic shadows, neon rim lights, and movie-like color grading.

---

## How to Use a LoRA in a Workflow

Once a LoRA is downloaded to your `D:\AI\models\loras` folder, you can activate it in two ways:

**Method 1: In the Prompt (Forge / WebUI)**
Simply add the LoRA trigger syntax to your prompt along with a strength weight (usually between `0.5` and `1.0`):
> `<lora:pixel_art_xl:1.0> a dog walking in a park, 16-bit style`

**Method 2: In ComfyUI**
Add a **"Load LoRA"** node to your workflow. 
1. Connect your Checkpoint model to the `Load LoRA` node.
2. Connect the `Load LoRA` node to your `CLIP Text Encode` (Prompt) and `KSampler`.
3. Select the downloaded LoRA from the dropdown inside the node.
