import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { APP_ENVIRONMENT } from '../config/environment.token';

export interface LoginRequest {
  email: string;
  password: string;
}

export interface RegisterRequest {
  email: string;
  password: string;
}

export interface AuthResponse {
  accessToken: string;
  refreshToken: string;
  expiresAtUtc: string;
  userId: string;
}

export interface DocumentItem {
  id: string;
  fileName: string;
  status: string;
  createdAt: string;
  /** When status is Failed, optional reason from the server. */
  failureReason?: string | null;
}

export interface QuizSession {
  quizId: string;
  totalQuestionCount: number;
  questions: (QuizQuestion | null)[];
}

export interface QuizQuestion {
  id: string;
  text: string;
  options: string[];
  conceptId?: string;
}

/** Response from get-question-by-index; status indicates Ready, Generating, or Failed. */
export interface GetQuizQuestionResponse {
  id: string;
  text?: string | null;
  options?: string[] | null;
  status: 'Ready' | 'Generating' | 'Failed';
  errorMessage?: string | null;
}

export interface SubmitQuizRequest {
  quizId: string;
  answers: { questionId: string; submittedAnswer?: string; submittedOptionIndex?: number }[];
}

export interface QuizResult {
  correctCount: number;
  totalCount: number;
  /** Per-question correct/incorrect and correct answer (name + 0-based option index). */
  questionResults?: {
    questionId: string;
    isCorrect: boolean;
    correctAnswer: string;
    correctOptionIndex: number;
  }[];
}

export interface WeakTopic {
  conceptId: string;
  /** Topic name from API (field is "name" in response). */
  name: string;
  masteryScore: number;
}

export interface AIHealthResponse {
  status: 'Healthy' | 'Degraded' | 'Unhealthy';
}

@Injectable({ providedIn: 'root' })
export class StudyPilotApiService {
  private readonly http = inject(HttpClient);
  private readonly env = inject(APP_ENVIRONMENT);

  private url(path: string): string {
    return `${this.env.apiBaseUrl.replace(/\/$/, '')}/${path.replace(/^\//, '')}`;
  }

  login(body: LoginRequest): Observable<AuthResponse> {
    return this.http.post<AuthResponse>(this.url('auth/login'), body);
  }

  register(body: RegisterRequest): Observable<AuthResponse> {
    return this.http.post<AuthResponse>(this.url('auth/register'), body);
  }

  refresh(refreshToken: string): Observable<AuthResponse> {
    return this.http.post<AuthResponse>(this.url('auth/refresh'), { refreshToken });
  }

  logout(refreshToken: string): Observable<void> {
    return this.http.post<void>(this.url('auth/logout'), { refreshToken });
  }

  uploadDocument(formData: FormData): Observable<{ documentId: string; status: string }> {
    return this.http.post<{ documentId: string; status: string }>(this.url('documents/upload'), formData);
  }

  getDocuments(): Observable<DocumentItem[]> {
    return this.http.get<DocumentItem[]>(this.url('documents'));
  }

  startQuiz(documentId: string): Observable<QuizSession> {
    return this.http.post<QuizSession>(this.url('quiz/start'), { documentId });
  }

  getQuizQuestion(quizId: string, questionIndex: number): Observable<GetQuizQuestionResponse> {
    return this.http.get<GetQuizQuestionResponse>(this.url(`quiz/${quizId}/questions/${questionIndex}`));
  }

  submitQuiz(body: SubmitQuizRequest): Observable<QuizResult> {
    return this.http.post<QuizResult>(this.url('quiz/submit'), body);
  }

  getWeakTopics(): Observable<WeakTopic[]> {
    return this.http.get<WeakTopic[]>(this.url('progress/weak-topics'));
  }

  getAIHealth(): Observable<AIHealthResponse> {
    return this.http.get<AIHealthResponse>(this.url('health/ai'));
  }

  createChatSession(documentId?: string | null): Observable<{ sessionId: string }> {
    return this.http.post<{ sessionId: string }>(this.url('chat/sessions'), { documentId: documentId ?? null });
  }

  sendChatMessage(sessionId: string, content: string): Observable<SendChatMessageResult> {
    return this.http.post<SendChatMessageResult>(this.url('chat/message'), { sessionId, content });
  }

  getChatHistory(sessionId: string, pageNumber = 1, pageSize = 50): Observable<GetChatHistoryResponse> {
    return this.http.get<GetChatHistoryResponse>(
      this.url(`chat/history/${sessionId}`),
      { params: { pageNumber, pageSize } }
    );
  }
}

export interface SendChatMessageResult {
  assistantMessageId: string;
  answer: string;
  citedChunkIds: string[];
}

export interface ChatMessageItem {
  messageId: string;
  role: string;
  content: string;
  createdAtUtc: string;
  citedChunkIds: string[];
}

export interface GetChatHistoryResponse {
  sessionId: string;
  totalCount: number;
  pageNumber: number;
  pageSize: number;
  messages: ChatMessageItem[];
}
