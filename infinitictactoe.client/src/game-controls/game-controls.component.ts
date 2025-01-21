import { Component, EventEmitter, OnInit, Output } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { CommonModule } from '@angular/common';
import { WebSocketService } from '../web-socket.service';
import { ClientEndMessage, ClientHelloMessage, MoveResultMessage, PlayerSide, ReadyMessage, StartMessage } from '../models';

@Component({
  selector: 'app-game-controls',
  templateUrl: './game-controls.component.html',
  styleUrls: ['./game-controls.component.css'],
  standalone: true,
  imports: [CommonModule, FormsModule]
})
export class GameControlsComponent implements OnInit {
  myScore: number = 0;

  otherPlayerName: string = '';
  otherPlayerScore: number = 0;

  gameStatus: string = 'Waiting for players...';
  mySide: PlayerSide | null = null;
  otherPlayerSide: PlayerSide | null = null;
  isYourTurn = false;

  nickname: string = '';
  private nicknameChangeTimeout: any;
  private nicknameChangeTimeoutMilliseconds = 1000;

  constructor(private webSocketService: WebSocketService) { }

  async ngOnInit() {
    const storedNickname = localStorage.getItem('nickname');
    if (storedNickname) {
      this.nickname = storedNickname;
    } else {
      this.nickname = `User${Math.floor(Math.random() * 100000000)}`;
      localStorage.setItem('nickname', this.nickname);
    }
    await this.webSocketService.connectionEstablished;
    this.webSocketService.sendMessage(new ClientHelloMessage(this.nickname));

    this.webSocketService.startReceived.subscribe(m => this.onStartReceived(m));
    this.webSocketService.moveResultReceived.subscribe(m => this.onMoveResultReceived(m));
  }

  onNicknameChange() {
    localStorage.setItem('nickname', this.nickname);
    if (this.nicknameChangeTimeout) {
      clearTimeout(this.nicknameChangeTimeout);
    }
    this.nicknameChangeTimeout = setTimeout(() => {
      this.webSocketService.sendMessage(new ClientHelloMessage(this.nickname));
    }, this.nicknameChangeTimeoutMilliseconds);
  }

  onStartGame() {
    console.log('Start Game button clicked');
    const readyMessage = new ReadyMessage();
    this.webSocketService.sendMessage(readyMessage);
  }

  onStartReceived(startMessage: StartMessage) {
    this.gameStatus = 'Game in progress';
    this.mySide = startMessage.side;
    this.otherPlayerSide = startMessage.side === PlayerSide.X ? PlayerSide.O : PlayerSide.X;

    this.otherPlayerName = this.mySide === PlayerSide.X
      ? startMessage.nicknameO
      : startMessage.nicknameX;

    this.isYourTurn = startMessage.yourTurn;
  }

  onMoveResultReceived(moveResult: MoveResultMessage) {
    this.myScore = this.mySide === PlayerSide.X
      ? moveResult.scoreX
      : moveResult.scoreO;
    this.otherPlayerScore = this.mySide === PlayerSide.X
      ? moveResult.scoreO
      : moveResult.scoreX;

    this.isYourTurn = moveResult.yourTurn;
  }

  onEndGame() {
    console.log('End Game button clicked');
    this.gameStatus = 'Waiting for players...';
    this.mySide = null;
    this.otherPlayerSide = null;
    this.isYourTurn = false;
    this.myScore = 0;
    this.otherPlayerScore = 0;
    this.webSocketService.sendMessage(new ClientEndMessage());
  }
}
