package com.learntoescape.llm.controller;

import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.springframework.http.MediaType;
import org.springframework.http.ResponseEntity;
import org.springframework.util.StopWatch;
import org.springframework.web.bind.annotation.CrossOrigin;
import org.springframework.web.bind.annotation.PostMapping;
import org.springframework.web.bind.annotation.RequestBody;
import org.springframework.web.bind.annotation.RequestMapping;
import org.springframework.web.bind.annotation.RestController;

import com.learntoescape.dto.RoomDTO;
import com.learntoescape.dto.RoomGenerationRequest;
import com.learntoescape.service.EscapeRoomGenerationService;

import jakarta.validation.Valid;

/**
 * Capa web del microservicio: punto de entrada HTTP para la generación
 * de salas educativas por IA.
 *
 * <p>Responsabilidades estrictas de esta clase:
 * <ul>
 *   <li>Recibir el payload, disparar la validación de Bean Validation
 *       ({@link Valid}) y rechazar la petición antes de tocar el LLM
 *       si el input es incorrecto.</li>
 *   <li>Delegar la orquestación completa a {@link EscapeRoomGenerationService}
 *       (que a su vez delega la red a {@code OllamaClient}).</li>
 *   <li>Medir la latencia <em>end-to-end</em> del endpoint para observabilidad.</li>
 * </ul>
 *
 * <p>Esta clase <b>no</b> captura excepciones: todos los fallos se propagan
 * al {@code GlobalExceptionHandler}, que los traduce a {@code ProblemDetail}
 * (RFC 7807) con códigos HTTP apropiados.</p>
 */
@RestController
@RequestMapping(path = "/api/rooms", produces = MediaType.APPLICATION_JSON_VALUE)
@CrossOrigin(originPatterns = { "http://localhost:*", "http://127.0.0.1:*" })
public class EscapeRoomController {

	private static final Logger log = LoggerFactory.getLogger(EscapeRoomController.class);

	private final EscapeRoomGenerationService generationService;

	public EscapeRoomController(EscapeRoomGenerationService generationService) {
		this.generationService = generationService;
	}

	/**
	 * Genera una sala educativa a partir de los parámetros del cliente.
	 *
	 * <p>Devuelve {@code 200 OK} con el {@link RoomDTO} canónico si el LLM
	 * produce un JSON válido. Cualquier fallo se canaliza al advice global:
	 * <ul>
	 *   <li>{@code 400} si la petición no pasa validación.</li>
	 *   <li>{@code 502} si la IA alucina JSON inválido tras 3 intentos.</li>
	 *   <li>{@code 503} si no se puede contactar con Ollama.</li>
	 *   <li>{@code 500} para cualquier otro imprevisto.</li>
	 * </ul>
	 *
	 * @param request payload validado por Bean Validation.
	 * @return sala generada, serializada en JSON.
	 */
	@PostMapping(path = "/generate", consumes = MediaType.APPLICATION_JSON_VALUE)
	public ResponseEntity<RoomDTO> generateRoom(@Valid @RequestBody RoomGenerationRequest request) {
		log.info("Solicitud de generación de sala recibida (topic='{}', difficulty='{}', puzzles={})",
				request.topic(), request.difficulty(), request.puzzleCount());

		StopWatch stopWatch = new StopWatch("generateRoom");
		stopWatch.start();

		RoomDTO room = generationService.generateValidatedRoom(request);

		stopWatch.stop();
		log.info("Sala generada correctamente en {} ms (topic='{}', difficulty='{}', theme='{}')",
				stopWatch.getTotalTimeMillis(),
				request.topic(),
				request.difficulty(),
				room.theme());

		return ResponseEntity.ok(room);
	}
}
