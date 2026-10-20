package com.arvirtuallab.text;

import com.google.mlkit.common.model.DownloadConditions;
import com.google.mlkit.nl.translate.TranslateLanguage;
import com.google.mlkit.nl.translate.Translation;
import com.google.mlkit.nl.translate.Translator;
import com.google.mlkit.nl.translate.TranslatorOptions;

import java.util.ArrayList;
import java.util.List;

/**
 * English to Telugu with Google ML Kit, on the phone. The model downloads once (any network), then works
 * offline. Called from MlKitTranslator.cs; results come back through Callback on an Android thread.
 */
public final class MlKitTranslator {
    public interface Callback {
        void onResult(String key, String translated);
        void onFailure(String key, String error);
    }

    private static Translator translator;
    private static boolean ready;
    private static boolean downloading;
    private static final List<String[]> waiting = new ArrayList<>();
    private static Callback callback;

    private MlKitTranslator() {}

    public static synchronized void translate(String key, String text, Callback cb) {
        callback = cb;
        if (translator == null) {
            translator = Translation.getClient(new TranslatorOptions.Builder()
                    .setSourceLanguage(TranslateLanguage.ENGLISH)
                    .setTargetLanguage(TranslateLanguage.TELUGU)
                    .build());
        }
        if (ready) {
            run(key, text);
            return;
        }
        waiting.add(new String[] { key, text });
        if (downloading) return;
        downloading = true;
        translator.downloadModelIfNeeded(new DownloadConditions.Builder().build())
                .addOnSuccessListener(unused -> {
                    synchronized (MlKitTranslator.class) {
                        ready = true;
                        downloading = false;
                        for (String[] w : waiting) run(w[0], w[1]);
                        waiting.clear();
                    }
                })
                .addOnFailureListener(e -> {
                    synchronized (MlKitTranslator.class) {
                        downloading = false;
                        for (String[] w : waiting) callback.onFailure(w[0], "model download: " + e.getMessage());
                        waiting.clear();
                    }
                });
    }

    private static void run(String key, String text) {
        final Callback cb = callback;
        translator.translate(text)
                .addOnSuccessListener(result -> cb.onResult(key, result))
                .addOnFailureListener(e -> cb.onFailure(key, String.valueOf(e.getMessage())));
    }
}
