import { Injectable, EventEmitter } from '@angular/core';
import { WebSocketSubject } from 'rxjs/webSocket';
import {
  ClientHelloMessage,
  ServerHelloMessage,
  MoveMessage,
  StartMessage,
  EndMessage,
  MoveResultMessage,
  TypedMessage,
  MessageType,
} from './models';

@Injectable({
  providedIn: 'root',
})
export class WebSocketService {
  private socket$: WebSocketSubject<any> | undefined;
  private resolveConnectionEstablished: ((value: void | PromiseLike<void>) => void) | undefined;
  public moveResultReceived: EventEmitter<MoveResultMessage> = new EventEmitter();
  public startReceived: EventEmitter<StartMessage> = new EventEmitter();
  public endReceived: EventEmitter<EndMessage> = new EventEmitter();
  public connectionEstablished: Promise<void> = new Promise((resolve) => this.resolveConnectionEstablished = resolve);


  connect() {
    const nickname = 'Your nickname';

    this.socket$ = new WebSocketSubject({
      url: 'wss://localhost:7146/ws',
      openObserver: {
        next: () => {
          console.log('Connection ok');
          //this.sendHelloMessage(nickname);
          if(this.resolveConnectionEstablished)
            this.resolveConnectionEstablished();
        },
      },
    });
    console.log('start connecting');

    this.socket$.subscribe({
      next: (message) => this.handleMessage(message),
      error: (err) => {
        console.error('some error');
        console.error(err);
      },
      complete: () => {
        console.warn('Completed!');
      },
    });
    console.log('subscribed to ws');
  }

  disconnect() {
    if (this.socket$) {
      this.socket$.complete();
      this.socket$ = undefined;
      console.log('WebSocket connection closed.');
    } else {
      console.error('WebSocket connection is not established.');
    }
  }

  private handleMessage(message: TypedMessage) {
    console.warn(`message received of type ${message.type}`);
    switch (message.type) {
      case MessageType.ServerHello:
        console.log('Hello received');
        break;
      case MessageType.MoveResult:
        const move = message as MoveResultMessage;
        console.log(`Move received: ${move.x}, ${move.y}, success=${move.success}, ${move.message}, ${move.scoreX}, ${move.scoreO}`);
        console.log(`Your turn: ${move.yourTurn}`);
        this.moveResultReceived.emit(move);
        break;
      case MessageType.Start:
        const start = message as StartMessage;
        console.log(`Start received: ${start.side}, Your turn: ${start.yourTurn}`);
        this.startReceived.emit(start);
        break;
      case MessageType.End:
        const end = message as EndMessage;
        console.log(`End received: ${end.scoreX}, ${end.scoreO}`);
        this.endReceived.emit(end);
        break;
      default:
        console.error('Unknown message type:', message.type);
    }
  }

  private sendHelloMessage(nickname: string) {
    const helloMessage = new ClientHelloMessage(nickname);
    this.sendMessage(helloMessage);
  }

  sendMessage(message: TypedMessage) {
    if (this.socket$) {
      this.socket$.next(message);
    } else {
      console.error('WebSocket connection is not established.');
    }
  }
}
