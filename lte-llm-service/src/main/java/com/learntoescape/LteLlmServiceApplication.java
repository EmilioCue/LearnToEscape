package com.learntoescape;

import org.springframework.boot.SpringApplication;
import org.springframework.boot.autoconfigure.SpringBootApplication;
import org.springframework.retry.annotation.EnableRetry;

/**
 * Punto de entrada del microservicio {@code lte-llm-service}.
 *
 * <p>Ubicada en el paquete raíz {@code com.learntoescape} para que el component-scan
 * cubra los subpaquetes hermanos {@code dto}, {@code client} y {@code llm.controller}.</p>
 *
 * <p>{@link EnableRetry} habilita el procesamiento de {@code @Retryable} y {@code @Recover}
 * en el que se apoya el cliente de Ollama para resiliencia frente a fallos de red.</p>
 */
@EnableRetry
@SpringBootApplication
public class LteLlmServiceApplication {

	public static void main(String[] args) {
		SpringApplication.run(LteLlmServiceApplication.class, args);
	}

}
