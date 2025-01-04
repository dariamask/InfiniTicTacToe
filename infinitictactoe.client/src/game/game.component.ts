import { Component, EventEmitter, OnInit, Output } from '@angular/core';
import { WebSocketService } from '../web-socket.service';
import { BoardComponent } from '../board/board.component';
import { PlayerListComponent } from '../player-list/player-list.component';
import { ChatComponent } from '../chat/chat.component';
import { MoveResultMessage, StartMessage, EndMessage, PlayerSide } from '../models';

@Component({
  selector: 'app-game',
  templateUrl: './game.component.html',
  styleUrls: ['./game.component.css'],
  imports: [
    BoardComponent,
    PlayerListComponent,
    ChatComponent,
  ],
})
export class GameComponent implements OnInit {
  @Output() moveResultReceived: EventEmitter<MoveResultMessage> = new EventEmitter();
  @Output() startReceived: EventEmitter<StartMessage> = new EventEmitter();
  @Output() endReceived: EventEmitter<EndMessage> = new EventEmitter();

  playerSide: PlayerSide | undefined;
  yourTurn: boolean = false;

  constructor(private webSocketService: WebSocketService) { }

  ngOnInit() {
    this.webSocketService.moveResultReceived.subscribe((message: MoveResultMessage) => {
      console.log('MoveResultMessage received:', message);
      this.moveResultReceived.emit(message);
    });

    this.webSocketService.startReceived.subscribe((message: StartMessage) => {
      console.log('StartMessage received:', message);
      this.playerSide = message.side;
      this.yourTurn = message.yourTurn;
      this.startReceived.emit(message);
    });

    this.webSocketService.endReceived.subscribe((message: EndMessage) => {
      console.log('EndMessage received:', message);
      this.endReceived.emit(message);
    });

    this.webSocketService.connect();
  }

  onMoveResultReceived(message: MoveResultMessage) {
    // Propagate the event to the BoardComponent
    this.moveResultReceived.emit(message);
  }
}
