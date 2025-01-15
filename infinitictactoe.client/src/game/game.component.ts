import { Component, EventEmitter, OnInit, Output } from '@angular/core';
import { WebSocketService } from '../web-socket.service';
import { BoardComponent } from '../board/board.component';
import { PlayerListComponent } from '../player-list/player-list.component';
import { ChatComponent } from '../chat/chat.component';
import { MoveResultMessage, StartMessage, EndMessage, PlayerSide } from '../models';
import { GameControlsComponent } from '../game-controls/game-controls.component'; // Import the GameControlsComponent

@Component({
  selector: 'app-game',
  templateUrl: './game.component.html',
  styleUrls: ['./game.component.css'],
  imports: [
    BoardComponent,
    PlayerListComponent,
    ChatComponent,
    GameControlsComponent,
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
    this.webSocketService.connect();
  }

  onMoveResultReceived(message: MoveResultMessage) {
    // Propagate the event to the BoardComponent
    this.moveResultReceived.emit(message);
  }
}
