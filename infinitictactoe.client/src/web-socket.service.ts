import { Injectable } from '@angular/core';
import { WebSocketSubject } from 'rxjs/webSocket';

@Injectable({
  providedIn: 'root'
})
export class WebSocketService {
  private socket$: WebSocketSubject<any> | undefined;

  connect() {
    this.socket$ = new WebSocketSubject('ws://your-websocket-url');
    this.socket$.subscribe({
      next: (message) => this.handleMessage(message),
      error: (err) => console.error(err),
      complete: () => console.warn('Completed!')
    });
  }

  private handleMessage(message: any) {
    // Handle incoming WebSocket messages
  }

  sendMessage(message: any) {
    if (this.socket$) {
      this.socket$.next(message);
    } else {
      console.error('WebSocket connection is not established.');
    }
  }
}
